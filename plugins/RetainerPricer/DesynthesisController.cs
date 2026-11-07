using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Item = Lumina.Excel.Sheets.Item;

namespace RetainerPricer;

internal sealed unsafe class DesynthesisController(PluginConfig config, NativeMarketBridge bridge,
    IDataManager data, ICondition condition, IObjectTable objects, IGameGui gui, Func<bool> otherBusy)
{
    private static readonly InventoryType[] Bags = [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4];
    private static readonly InventoryType[] Armoury = [InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand,
        InventoryType.ArmoryHead, InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets, InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings];
    private (InventoryType Source, int SourceSlot, InventoryType Bag, int Slot, uint RawId, uint Quantity, string Materia)? transfer;
    private DateTimeOffset transferDeadline;
    private DateTimeOffset? transferStableSince;
    private bool transferActionSent;

    private bool TryGetProtections(out HashSet<uint> ids, out string error)
    {
        if (!ItemProtection.TryGetExcludedItemIds(bridge, config, out ids, out var saved, out error)) return false;
        // Gearsets store HQ IDs; inventory candidates use base IDs. Protect both qualities conservatively.
        ids.UnionWith(saved.Select(id => id >= 1000000 ? id - 1000000 : id));
        return true;
    }

    private bool CanDesynthesize(uint id)
    {
        if (data.GetExcelSheet<Item>().GetRowOrDefault(id) is not { } item || item.Desynth <= 0 || !item.ClassJobRepair.IsValid) return false;
        var index = item.ClassJobRepair.Value.ExpArrayIndex;
        var player = PlayerState.Instance();
        return player != null && index >= 0 && index < player->ClassJobLevels.Length && player->ClassJobLevels[index] >= 30;
    }

    // Move just one eligible copy, then wait for the server inventory update before processing it.
    private bool PullArmouryItem(InventoryManager* inventory, HashSet<uint> protectedIds, DateTimeOffset now)
    {
        foreach (var type in Armoury)
        {
            var source = inventory->GetInventoryContainer(type);
            if (source == null || !source->IsLoaded) { Stop("Armoury chest is loading; retry when ready."); return true; }
            for (int i = 0; i < source->Size; i++)
            {
                var item = source->GetInventorySlot(i);
                if (item == null || item->IsEmpty() || item->IsCollectable()) continue;
                uint id = item->GetBaseItemId();
                if (!config.DesynthesisItemIds.Contains(id) || protectedIds.Contains(id) || !CanDesynthesize(id)) continue;
                foreach (var bagType in Bags)
                {
                    var bag = inventory->GetInventoryContainer(bagType);
                    if (bag == null || !bag->IsLoaded) { Stop("Carried inventory is loading."); return true; }
                    for (int j = 0; j < bag->Size; j++)
                    {
                        var destination = bag->GetInventorySlot(j);
                        if (destination == null || !destination->IsEmpty()) continue;
                        transfer = (type, i, bagType, j, item->ItemId, item->GetQuantity(), Melds(item));
                        transferStableSince = null;
                        transferActionSent = false;
                        transferDeadline = now.AddSeconds(15);
                        var context = AgentInventoryContext.Instance();
                        if (context == null) { Stop("Armoury transfer stopped: inventory context agent unavailable."); return true; }
                        context->OpenForItemSlot(type, i, 0, 0);
                        DesynthesisDiagnostic($"Opening native Return to Inventory action: item={id}, source={type}, sourceSlot={i}.");
                        Status = $"Moving {data.GetExcelSheet<Item>().GetRow(id).Name} from armoury chest to inventory...";
                        return true;
                    }
                }
                Stop("Free a carried inventory slot to move listed armoury items for desynthesis.");
                return true;
            }
        }
        return false;
    }

    public bool IsRunning { get; private set; }
    public string Status { get; private set; } = "Ready.";
    private DateTimeOffset next, deadline;
    private (InventoryType Bag, int Slot, uint Id, uint Quantity)? pending;
    private int completed;
    private bool desynthesisConfirmed;
    private int confirmationAttempts;
    private DateTimeOffset confirmationSentAt;
    private bool desynthesisCastObserved;
    private string desynthesisDiagnostic = "";
    private (InventoryType Bag, int Slot, uint Id, bool Hq)? extractionItem;
    private bool extractionConfirmed, extractionMenuSelected;
    private DateTimeOffset extractionDeadline;
    private void UpdateExtraction(InventoryManager* inventory, DateTimeOffset now)
    {
        if (extractionItem is not { } item) return;
        var bag = inventory->GetInventoryContainer(item.Bag);
        var slot = bag == null || !bag->IsLoaded ? null : bag->GetInventorySlot(item.Slot);
        if (slot == null || slot->IsEmpty() || slot->GetBaseItemId() != item.Id || slot->IsHighQuality() != item.Hq)
        { Stop("Materia extraction stopped: selected item changed."); return; }
        if (!TryGetProtections(out var protectedIds, out var error)) { Stop(error); return; }
        if (protectedIds.Contains(item.Id) || !config.DesynthesisItemIds.Contains(item.Id))
        { Stop("Materia extraction stopped: item is now protected or removed from the list."); return; }
        if (slot->GetSpiritbondOrCollectability() < 10000)
        { extractionItem = null; next = now; Status = "Materia extracted; continuing desynthesis."; return; }
        if (!extractionMenuSelected)
        {
            if (now >= extractionDeadline) { Stop("Extract Materia menu did not become ready; no desynthesis was attempted."); return; }
            if (!MateriaRetrieval.TrySelect(gui, data, item.Bag, item.Slot, slot, out var menuPending, out var menuError, extraction: true))
            { if (!menuPending) Stop(menuError + " No desynthesis was attempted."); return; }
            extractionMenuSelected = true; extractionDeadline = now.AddSeconds(15);
            return;
        }
        if (!extractionConfirmed)
        {
            var dialog = gui.GetAddonByName<FFXIVClientStructs.FFXIV.Client.UI.AddonMaterializeDialog>("MaterializeDialog");
            if (dialog != null && NativeUiButton.Click((AtkUnitBase*)dialog, dialog->YesButton)) extractionConfirmed = true;
        }
        if (now >= extractionDeadline) Stop("Materia extraction was not confirmed. Check the game error or inventory space; item was not desynthesized.");
    }
    public bool WaitingForMateria { get; private set; }
    public string MateriaItemName { get; private set; } = "";
    private (InventoryType Bag, int Slot, uint Id, bool Hq, string Melds)? materiaItem;
    private readonly HashSet<(InventoryType, int, uint)> skippedMateria = [];
    private bool retrieveApproved, retrievalSent, retrievalConfirmed, retrievalMenuOpened;
    private string retrievalBefore = "";
    private DateTimeOffset retrievalDeadline;
    public void AnswerMateria(bool retrieve)
    {
        if (!WaitingForMateria || materiaItem is not { } item) return;
        WaitingForMateria = false;
        if (retrieve) retrieveApproved = true;
        else { skippedMateria.Add((item.Bag,item.Slot,item.Id)); materiaItem = null; }
    }
    private static string Melds(InventoryItem* slot) => string.Join(",", Enumerable.Range(0,5).Select(i => $"{slot->Materia[i]}:{slot->MateriaGrades[i]}"));

    public bool WaitingForBroth { get; private set; }
    private bool brothHandled, brothRequested;
    private DateTimeOffset brothDeadline;
    public void ContinueWithoutBroth() { WaitingForBroth = false; brothHandled = true; }
    private bool HasBrothBuff => objects.LocalPlayer?.StatusList.Any(status => status.StatusId == 48
        && status.Param % 10000 == 270 && status.RemainingTime > 5) == true;
    private bool EnsureBroth(InventoryManager* inventory, DateTimeOffset now)
    {
        if (!config.UseBaconBroth || brothHandled) return true;
        if (HasBrothBuff) { brothHandled = true; brothRequested = false; return true; }
        if (brothRequested)
        {
            if (now >= brothDeadline) Stop("Bacon Broth buff was not confirmed. Check the game error before retrying.");
            return false;
        }
        if (WaitingForBroth) return false;
        bool nq = false, hq = false;
        foreach (var type in Bags)
        {
            var bag = inventory->GetInventoryContainer(type);
            if (bag == null || !bag->IsLoaded) { Stop("Carried inventory is loading."); return false; }
            for (int i = 0; i < bag->Size; i++)
            {
                var item = bag->GetInventorySlot(i);
                if (item == null || item->IsEmpty() || item->GetBaseItemId() != 9335) continue;
                if (item->IsHighQuality()) hq = true; else nq = true;
            }
        }
        if (!hq && !nq)
        {
            WaitingForBroth = true;
            Status = "Bacon Broth not found. Choose Continue without Bacon Broth or Stop.";
            return false;
        }
        if (!ActionManager.Instance()->UseAction(ActionType.Item, hq ? 1009335u : 9335u, 0xE0000000, 65535))
        { Stop("Bacon Broth could not be used. No items were desynthesized by this request."); return false; }
        brothRequested = true; brothDeadline = now.AddSeconds(15);
        Status = hq ? "Using HQ Bacon Broth..." : "Using NQ Bacon Broth...";
        return false;
    }
    public IReadOnlyList<uint> Snapshot(bool saddlebag, out string message)
    {
        var inventory = InventoryManager.Instance();
        if (bridge.IsClientStateUnavailable || inventory == null)
        { message = "Log in before grabbing inventory."; return []; }
        InventoryType[] containers = saddlebag
            ? [InventoryType.SaddleBag1, InventoryType.SaddleBag2, InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2]
            : Bags;
        HashSet<uint> ids = [];
        int loaded = 0;
        foreach (var type in containers)
        {
            var bag = inventory->GetInventoryContainer(type);
            if (bag == null || !bag->IsLoaded) continue;
            loaded++;
            for (int i = 0; i < bag->Size; i++)
            {
                var slot = bag->GetInventorySlot(i);
                if (slot == null || slot->IsEmpty() || slot->IsCollectable()) continue;
                uint id = slot->GetBaseItemId();
                if (data.GetExcelSheet<Item>().GetRowOrDefault(id) is { } item && item.Desynth > 0) ids.Add(id);
            }
        }
        message = loaded == 0 ? "Inventory is not loaded. Open the saddlebag, then grab again."
            : $"Captured {ids.Count} desynthesizable item type(s)." + (saddlebag
                ? " Open the saddlebag and refresh if anything is missing. Move selected items into carried bags before running."
                : " Use Add to include an item in your list.");
        return ids.OrderBy(id => data.GetExcelSheet<Item>().GetRow(id).Name.ToString()).ToArray();
    }
    public void Stop(string reason = "Desynthesis stopped.") { IsRunning = false; transfer = null; extractionItem = null; pending = null; WaitingForBroth = false; WaitingForMateria = false; materiaItem = null; retrieveApproved = retrievalSent = retrievalMenuOpened = false; Status = reason; }
    public void Start()
    {
        if (otherBusy()) { Status = "Wait for other Retainer Pricer operations to finish."; return; }
        if (bridge.IsClientStateUnavailable || !QuestManager.IsQuestComplete(65688))
        { Status = "Log in and unlock desynthesis first."; return; }
        if (config.DesynthesisItemIds.Count == 0) { Status = "Add items to the desynthesis list first."; return; }
        if (!TryGetProtections(out _, out var error)) { Status = error; return; }
        skippedMateria.Clear(); materiaItem = null; retrieveApproved = retrievalSent = retrievalMenuOpened = WaitingForMateria = false;
        extractionItem = null;
        transfer = null;
        completed = 0; pending = null; next = DateTimeOffset.MinValue;
        brothHandled = brothRequested = WaitingForBroth = false;
        IsRunning = true; Status = "Desynthesis started. Close retainer and other interaction windows.";
    }
    private void UpdateMateria(InventoryManager* inventory, DateTimeOffset now)
    {
        if (materiaItem is not { } item || WaitingForMateria) return;
        var bag = inventory->GetInventoryContainer(item.Bag);
        var slot = bag == null || !bag->IsLoaded ? null : bag->GetInventorySlot(item.Slot);
        if (slot == null || slot->IsEmpty() || slot->GetBaseItemId() != item.Id || slot->IsHighQuality() != item.Hq)
        { Stop("Materia retrieval stopped: the selected inventory item changed."); return; }
        if (!TryGetProtections(out var protectedIds, out var error))
        { Stop(error); return; }
        if (protectedIds.Contains(item.Id) || !config.DesynthesisItemIds.Contains(item.Id))
        { Stop("Materia retrieval stopped: this item is now protected or removed from the list."); return; }
        string current = Melds(slot);
        if (!retrieveApproved) return;
        if (retrievalSent)
        {
            if (current != retrievalBefore)
            {
                retrievalSent = retrievalMenuOpened = false;
                materiaItem = item with { Melds = current };
                next = now;
            }
            else if (!retrievalConfirmed && MateriaRetrieval.TryConfirm(gui, item.Bag, item.Slot, slot)) retrievalConfirmed = true;
            else if (now >= retrievalDeadline) Stop("Materia retrieval did not complete. Check the game error and inventory space.");
            return;
        }
        bool melded = false;
        for (int m=0;m<5;m++) if (slot->Materia[m]!=0) melded = true;
        if (!melded) { materiaItem = null; retrieveApproved = false; Status = "Materia removed; continuing desynthesis."; return; }
        if (current != item.Melds) { Stop("Materia retrieval stopped: the selected item's melds changed."); return; }
        if (retrievalMenuOpened)
        {
            if (now >= retrievalDeadline) { Stop("Retrieve Materia menu did not become ready; no desynthesis was attempted."); return; }
            if (!MateriaRetrieval.TrySelect(gui, data, item.Bag, item.Slot, slot, out var menuPending, out var menuError))
            { if (!menuPending) Stop(menuError + " No desynthesis was attempted."); return; }
            retrievalMenuOpened = false; retrievalBefore = current; retrievalSent = true; retrievalConfirmed = false;
            retrievalDeadline = now.AddSeconds(15);
            Status = $"Retrieving materia from {MateriaItemName}...";
            return;
        }
        if (condition[ConditionFlag.Occupied] || condition[ConditionFlag.Occupied39] || condition[ConditionFlag.Casting]
            || ActionManager.Instance() == null || ActionManager.Instance()->AnimationLock > 0) return;
        if (inventory->GetEmptySlotsInBag() < 5) { Stop("Free at least five carried inventory slots before retrieving materia."); return; }
        if (!MateriaRetrieval.TryOpen(gui, item.Bag, item.Slot, out var retrievalError))
        { Stop(retrievalError + " No desynthesis was attempted."); return; }
        retrievalMenuOpened = true; retrievalDeadline = now.AddSeconds(15);
        Status = $"Retrieving materia from {MateriaItemName}...";
    }

    private void DesynthesisDiagnostic(string message)
    {
        if (message == desynthesisDiagnostic) return;
        desynthesisDiagnostic = message;
        bridge.LogAutomationDiagnostic(message);
    }

    public void Update()
    {
        if (!IsRunning) return;
        if (bridge.IsClientStateUnavailable || otherBusy() || condition[ConditionFlag.InCombat])
        { Stop("Desynthesis stopped: character unavailable, in combat, or another operation started."); return; }
        var now = DateTimeOffset.UtcNow;
        if (now < next) return;
        next = now.AddMilliseconds(100);
        var inventory = InventoryManager.Instance();
        if (inventory == null) { Stop("Inventory unavailable."); return; }
        if (transfer is { } moving)
        {
            var source = inventory->GetInventoryContainer(moving.Source);
            var destination = inventory->GetInventoryContainer(moving.Bag);
            if (source == null || destination == null || !source->IsLoaded || !destination->IsLoaded)
            { Stop("Inventory became unavailable during armoury transfer; no desynthesis requested."); return; }
            var before = source->GetInventorySlot(moving.SourceSlot);
            var after = destination->GetInventorySlot(moving.Slot);
            if (!transferActionSent)
            {
                if (now >= transferDeadline) { Stop("Armoury Return to Inventory menu timed out; no desynthesis requested."); return; }
                if (before == null || before->ItemId != moving.RawId || before->GetQuantity() != moving.Quantity || Melds(before) != moving.Materia)
                { Stop("Armoury item changed before Return to Inventory; no transfer requested."); return; }
                if (!TryGetProtections(out var protectedNow, out var transferProtectionError)) { Stop(transferProtectionError); return; }
                if (protectedNow.Contains(before->GetBaseItemId()) || !config.DesynthesisItemIds.Contains(before->GetBaseItemId()))
                { Stop("Armoury item became protected or was removed from the desynthesis list."); return; }
                if (MateriaRetrieval.TrySelect(gui, data, moving.Source, moving.SourceSlot, before,
                    out var menuPending, out var menuError, actionOverride: "Return to Inventory"))
                {
                    transferActionSent = true;
                    transferDeadline = now.AddSeconds(15);
                    DesynthesisDiagnostic($"Native Return to Inventory requested: item={moving.RawId}, source={moving.Source}, sourceSlot={moving.SourceSlot}; waiting for verified transfer.");
                }
                else if (!menuPending) Stop("Armoury transfer stopped: " + menuError);
                return;
            }
            bool operationPending = false;
            foreach (var operation in inventory->PendingOperations)
                if (!operation.IsEmpty && operation.SourceInventoryType == moving.Source
                    && operation.SourceInventorySlot == moving.SourceSlot
                    && operation.DestinationInventoryType == moving.Bag
                    && operation.DestinationInventorySlot == moving.Slot)
                { operationPending = true; break; }
            if (!operationPending && before != null && before->IsEmpty() && after != null && after->ItemId == moving.RawId
                && after->GetQuantity() == moving.Quantity && Melds(after) == moving.Materia)
            {
                transferStableSince ??= now;
                if (now < transferStableSince.Value.AddMilliseconds(1500))
                {
                    Status = "Waiting for armoury transfer to remain stable before desynthesis.";
                    return;
                }
                transfer = null;
                transferStableSince = null;
                DesynthesisDiagnostic($"Armoury transfer stable for 1500 ms with no matching pending operation: item={after->GetBaseItemId()}, source={moving.Source}, sourceSlot={moving.SourceSlot}, bag={moving.Bag}, slot={moving.Slot}, quantity={after->GetQuantity()}.");
                Status = "Armoury transfer confirmed; checking item protections before desynthesis.";
            }
            else
            {
                if (transferStableSince != null)
                    DesynthesisDiagnostic($"Armoury transfer changed during settling: source={moving.Source}, sourceSlot={moving.SourceSlot}, bag={moving.Bag}, slot={moving.Slot}, operationPending={operationPending}; no desynthesis requested.");
                transferStableSince = null;
                if (now >= transferDeadline)
                    Stop($"Armoury transfer did not settle: item={moving.RawId}, source={moving.Source}, sourceSlot={moving.SourceSlot}, bag={moving.Bag}, slot={moving.Slot}, sourceItem={(before == null ? 0 : before->ItemId)}, destinationItem={(after == null ? 0 : after->ItemId)}, operationPending={operationPending}. No desynthesis requested.");
            }
            return;
        }
        if (extractionItem != null) { UpdateExtraction(inventory, now); return; }
        if (materiaItem != null) { UpdateMateria(inventory, now); return; }
        if (pending is { } wait)
        {
            var bag = inventory->GetInventoryContainer(wait.Bag);
            if (bag == null || !bag->IsLoaded) { Stop("Inventory is loading."); return; }
            var slot = bag->GetInventorySlot(wait.Slot);
            if (slot != null && (slot->GetBaseItemId() != wait.Id || slot->GetQuantity() < wait.Quantity))
            { completed++; pending = null; next = now; Status = $"Desynthesized {completed} item(s)."; }
            else if (now >= deadline)
                Stop($"Desynthesis stopped: item {wait.Id} in {wait.Bag}, slot {wait.Slot} was not consumed. {desynthesisDiagnostic} Check game errors or inventory space.");
            else if (desynthesisConfirmed)
            {
                if (condition[ConditionFlag.Casting])
                {
                    desynthesisCastObserved = true;
                    DesynthesisDiagnostic($"Desynthesis cast observed: item={wait.Id}, bag={wait.Bag}, slot={wait.Slot}; waiting for consumption.");
                }
                else if (!desynthesisCastObserved && now >= confirmationSentAt.AddSeconds(3))
                {
                    var dialog = gui.GetAddonByName<AddonSalvageDialog>("SalvageDialog");
                    // Retry only an unaccepted confirmation. Never reopen a closed dialog or
                    // repeat after a cast; the next update revalidates identity and protections.
                    if (dialog != null && dialog->IsReady && dialog->IsVisible
                        && dialog->DesynthesizeButton != null && dialog->DesynthesizeButton->IsEnabled
                        && confirmationAttempts < 3)
                    {
                        desynthesisConfirmed = false;
                        DesynthesisDiagnostic($"Desynthesis did not start after confirmation attempt {confirmationAttempts}: item={wait.Id}, bag={wait.Bag}, slot={wait.Slot}; rechecking the open dialog before retry.");
                    }
                    else
                    {
                        var agent = AgentSalvage.Instance();
                        var current = slot == null ? "carried slot unavailable" : $"carriedItem={slot->ItemId}, carriedQuantity={slot->GetQuantity()}";
                        var selected = agent == null ? "agent unavailable" :
                            $"agentItem={agent->DesynthItemSlot.ItemId}, agentBag={agent->DesynthItemSlot.Container}, agentSlot={agent->DesynthItemSlot.Slot}, resultOpen={agent->IsSalvageResultAddonOpen}, resultItem={agent->DesynthItemId}";
                        DesynthesisDiagnostic($"Waiting for desynthesis consumption: item={wait.Id}, bag={wait.Bag}, slot={wait.Slot}, attempts={confirmationAttempts}, dialogVisible={dialog != null && dialog->IsVisible}, casting={condition[ConditionFlag.Casting]}, castObserved={desynthesisCastObserved}, {current}, {selected}.");
                    }
                }
            }
            else if (!desynthesisConfirmed)
            {
                if (!TryGetProtections(out var protectedNow, out var error)) { Stop(error); return; }
                if (protectedNow.Contains(wait.Id) || !config.DesynthesisItemIds.Contains(wait.Id))
                { Stop("Desynthesis stopped: selected item is now protected or removed from the list."); return; }
                var agent = AgentSalvage.Instance();
                var dialog = gui.GetAddonByName<AddonSalvageDialog>("SalvageDialog");
                if (agent == null || dialog == null || !dialog->IsVisible || !dialog->IsReady)
                { DesynthesisDiagnostic($"Waiting for desynthesis confirmation dialog: item={wait.Id}, bag={wait.Bag}, slot={wait.Slot}."); return; }
                if (agent->DesynthItemSlot.GetBaseItemId() != wait.Id
                    || agent->DesynthItemSlot.Container != wait.Bag || agent->DesynthItemSlot.Slot != wait.Slot
                    || slot == null || agent->DesynthItemSlot.ItemId != slot->ItemId
                    || Melds(&agent->DesynthItemSlot) != Melds(slot))
                { Stop($"Desynthesis confirmation does not match requested item {wait.Id} in {wait.Bag}, slot {wait.Slot}; no confirmation sent."); return; }
                // Acknowledge only the item warning on this verified desynthesis dialog.
                foreach (var entry in dialog->Checkboxes)
                {
                    if (entry.Type != SalvageDialogCheckboxType.Warning || entry.Checkbox == null) continue;
                    if (entry.Checkbox->IsChecked && entry.IsCheckedCached) continue;
                    if (!entry.Checkbox->IsEnabled || entry.Checkbox->OwnerNode == null || !entry.Checkbox->OwnerNode->IsVisible())
                    { DesynthesisDiagnostic($"Waiting for enabled desynthesis item warning: item={wait.Id}."); return; }
                    // The component's checked state and the dialog's cached acknowledgement are
                    // separate. Change the component first, then notify the registered addon
                    // listener just as a checkbox interaction does; never force-enable the button.
                    entry.Checkbox->SetChecked(true);
                    var notified = NativeUiButton.Click((AtkUnitBase*)dialog, (AtkComponentButton*)entry.Checkbox);
                    DesynthesisDiagnostic($"Desynthesis item warning requested: item={wait.Id}, checked={entry.Checkbox->IsChecked}, cached={entry.IsCheckedCached}, notified={notified}.");
                    return;
                }
                if (condition[ConditionFlag.Casting] || ActionManager.Instance() == null || ActionManager.Instance()->AnimationLock > 0)
                { DesynthesisDiagnostic($"Waiting for action readiness before desynthesis confirmation: item={wait.Id}, bag={wait.Bag}, slot={wait.Slot}."); return; }
                if (NativeUiButton.Click((AtkUnitBase*)dialog, dialog->DesynthesizeButton))
                {
                    desynthesisConfirmed = true;
                    confirmationAttempts++;
                    confirmationSentAt = now;
                    deadline = now.AddSeconds(15);
                    DesynthesisDiagnostic($"Desynthesis confirmation sent: item={wait.Id}, bag={wait.Bag}, slot={wait.Slot}.");
                    Status = $"Desynthesizing {data.GetExcelSheet<Item>().GetRow(wait.Id).Name} Â· {completed} completed.";
                }
                else DesynthesisDiagnostic($"Desynthesis dialog waiting: item={wait.Id}, buttonEnabled={dialog->DesynthesizeButton != null && dialog->DesynthesizeButton->IsEnabled}.");
            }
            return;
        }
        if (condition[ConditionFlag.Occupied] || condition[ConditionFlag.Occupied39] || condition[ConditionFlag.Casting]
            || condition[ConditionFlag.OccupiedInQuestEvent] || condition[ConditionFlag.OccupiedSummoningBell]
            || ActionManager.Instance() == null || ActionManager.Instance()->AnimationLock > 0) return;
        if (!TryGetProtections(out var protectedIds, out var protectionError))
        { Stop(protectionError); return; }
        foreach (var type in Bags)
        {
            var bag = inventory->GetInventoryContainer(type);
            if (bag == null || !bag->IsLoaded) { Stop("Inventory is loading."); return; }
            for (int i = 0; i < bag->Size; i++)
            {
                var slot = bag->GetInventorySlot(i);
                if (slot == null || slot->IsEmpty()) continue;
                uint id = slot->GetBaseItemId();
                if (!config.DesynthesisItemIds.Contains(id) || protectedIds.Contains(id) || slot->IsCollectable()) continue;
                var row = data.GetExcelSheet<Item>().GetRowOrDefault(id);
                if (row is not { } item || item.Desynth <= 0 || !item.ClassJobRepair.IsValid) continue;
                var index = item.ClassJobRepair.Value.ExpArrayIndex;
                var player = PlayerState.Instance();
                if (player == null || index < 0 || index >= player->ClassJobLevels.Length || player->ClassJobLevels[index] < 30) continue;
                if (skippedMateria.Contains((type,i,id))) continue;
                if (slot->GetSpiritbondOrCollectability() >= 10000)
                {
                    if (inventory->GetEmptySlotsInBag() < 1) { Stop("Free a carried inventory slot before extracting materia."); return; }
                    if (!MateriaRetrieval.TryOpen(gui, type, i, out var extractError, extraction: true))
                    { Stop("Materia extraction could not start: " + extractError + " No desynthesis was attempted."); return; }
                    extractionItem = (type, i, id, slot->IsHighQuality()); extractionConfirmed = extractionMenuSelected = false; extractionDeadline = now.AddSeconds(15);
                    Status = $"Extracting materia from {item.Name} before desynthesis...";
                    return;
                }
                // Ask for this exact melded copy before removing its materia.
                bool melded = false;
                for (int m = 0; m < 5; m++) if (slot->Materia[m] != 0) melded = true;
                if (melded)
                {
                    materiaItem = (type,i,id,slot->IsHighQuality(),Melds(slot));
                    MateriaItemName = item.Name.ToString();
                    WaitingForMateria = true;
                    retrieveApproved = retrievalSent = retrievalMenuOpened = false;
                    Status = $"Materia found on {MateriaItemName}. Choose Retrieve materia or Skip item.";
                    return;
                }
                if (!EnsureBroth(inventory, now)) return;
                var agent = AgentSalvage.Instance();
                if (agent == null) { Stop("Desynthesis agent unavailable."); return; }
                pending = (type, i, id, slot->GetQuantity()); deadline = now.AddSeconds(15);
                desynthesisConfirmed = false;
                confirmationAttempts = 0;
                desynthesisCastObserved = false;
                desynthesisDiagnostic = "";
                agent->SalvageItem(slot);
                DesynthesisDiagnostic($"Opening desynthesis dialog: item={id}, bag={type}, slot={i}, quantity={slot->GetQuantity()}.");
                Status = $"Preparing desynthesis for {item.Name} Â· {completed} completed.";
                return;
            }
        }
        if (PullArmouryItem(inventory, protectedIds, now)) return;
        Stop($"Desynthesis complete: {completed} item(s). Protected, declined and ineligible items skipped.");
    }
}
