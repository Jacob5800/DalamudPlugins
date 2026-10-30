using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Item = Lumina.Excel.Sheets.Item;

namespace RetainerPricer;

internal sealed unsafe class DesynthesisController(PluginConfig config, NativeMarketBridge bridge,
    IDataManager data, ICondition condition, IObjectTable objects, IGameGui gui, Func<bool> otherBusy)
{
    private static readonly InventoryType[] Bags = [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4];
    public bool IsRunning { get; private set; }
    public string Status { get; private set; } = "Ready.";
    private DateTimeOffset next, deadline;
    private (InventoryType Bag, int Slot, uint Id, uint Quantity)? pending;
    private int completed;
    public bool WaitingForMateria { get; private set; }
    public string MateriaItemName { get; private set; } = "";
    private (InventoryType Bag, int Slot, uint Id, bool Hq, string Melds)? materiaItem;
    private readonly HashSet<(InventoryType, int, uint)> skippedMateria = [];
    private bool retrieveApproved, retrievalSent, retrievalConfirmed;
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
    public void Stop(string reason = "Desynthesis stopped.") { IsRunning = false; pending = null; WaitingForBroth = false; WaitingForMateria = false; materiaItem = null; retrieveApproved = retrievalSent = false; Status = reason; }
    public void Start()
    {
        if (otherBusy()) { Status = "Wait for other Retainer Pricer operations to finish."; return; }
        if (bridge.IsClientStateUnavailable || !QuestManager.IsQuestComplete(65688))
        { Status = "Log in and unlock desynthesis first."; return; }
        if (config.DesynthesisItemIds.Count == 0) { Status = "Add items to the desynthesis list first."; return; }
        if (!ItemProtection.TryGetExcludedItemIds(bridge, config, out _, out _, out var error)) { Status = error; return; }
        skippedMateria.Clear(); materiaItem = null; retrieveApproved = retrievalSent = WaitingForMateria = false;
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
        if (!ItemProtection.TryGetExcludedItemIds(bridge, config, out var protectedIds, out _, out var error))
        { Stop(error); return; }
        if (protectedIds.Contains(item.Id) || !config.DesynthesisItemIds.Contains(item.Id))
        { Stop("Materia retrieval stopped: this item is now protected or removed from the list."); return; }
        string current = Melds(slot);
        if (!retrieveApproved) return;
        if (retrievalSent)
        {
            if (current != retrievalBefore)
            {
                retrievalSent = false;
                materiaItem = item with { Melds = current };
                next = now.AddSeconds(1);
            }
            else if (!retrievalConfirmed && MateriaRetrieval.TryConfirm(gui, item.Bag, item.Slot, slot)) retrievalConfirmed = true;
            else if (now >= retrievalDeadline) Stop("Materia retrieval did not complete. Check the game error and inventory space.");
            return;
        }
        bool melded = false;
        for (int m=0;m<5;m++) if (slot->Materia[m]!=0) melded = true;
        if (!melded) { materiaItem = null; retrieveApproved = false; Status = "Materia removed; continuing desynthesis."; return; }
        if (current != item.Melds) { Stop("Materia retrieval stopped: the selected item's melds changed."); return; }
        if (condition[ConditionFlag.Occupied] || condition[ConditionFlag.Occupied39] || condition[ConditionFlag.Casting]
            || ActionManager.Instance() == null || ActionManager.Instance()->AnimationLock > 0) return;
        if (inventory->GetEmptySlotsInBag() < 5) { Stop("Free at least five carried inventory slots before retrieving materia."); return; }
        if (!MateriaRetrieval.TryBegin(gui, data, item.Bag, item.Slot, slot))
        { Stop("Could not open and confirm materia retrieval for this item. No desynthesis was attempted."); return; }
        retrievalBefore = current; retrievalSent = true; retrievalConfirmed = false; retrievalDeadline = now.AddSeconds(15);
        Status = $"Retrieving materia from {MateriaItemName}...";
    }

    public void Update()
    {
        if (!IsRunning) return;
        if (bridge.IsClientStateUnavailable || otherBusy() || condition[ConditionFlag.InCombat])
        { Stop("Desynthesis stopped: character unavailable, in combat, or another operation started."); return; }
        var now = DateTimeOffset.UtcNow;
        if (now < next) return;
        next = now.AddMilliseconds(250);
        var inventory = InventoryManager.Instance();
        if (inventory == null) { Stop("Inventory unavailable."); return; }
        if (materiaItem != null) { UpdateMateria(inventory, now); return; }
        if (pending is { } wait)
        {
            var bag = inventory->GetInventoryContainer(wait.Bag);
            if (bag == null || !bag->IsLoaded) { Stop("Inventory is loading."); return; }
            var slot = bag->GetInventorySlot(wait.Slot);
            if (slot != null && (slot->GetBaseItemId() != wait.Id || slot->GetQuantity() < wait.Quantity))
            { completed++; pending = null; next = now.AddSeconds(1); Status = $"Desynthesized {completed} item(s)."; }
            else if (now >= deadline) Stop("Desynthesis stopped: item was not consumed. Check game errors or inventory space.");
            return;
        }
        if (condition[ConditionFlag.Occupied] || condition[ConditionFlag.Occupied39] || condition[ConditionFlag.Casting]
            || condition[ConditionFlag.OccupiedInQuestEvent] || condition[ConditionFlag.OccupiedSummoningBell]
            || ActionManager.Instance() == null || ActionManager.Instance()->AnimationLock > 0) return;
        if (!ItemProtection.TryGetExcludedItemIds(bridge, config, out var protectedIds, out _, out var protectionError))
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
                // Ask for this exact melded copy before removing its materia.
                bool melded = false;
                for (int m = 0; m < 5; m++) if (slot->Materia[m] != 0) melded = true;
                if (melded)
                {
                    materiaItem = (type,i,id,slot->IsHighQuality(),Melds(slot));
                    MateriaItemName = item.Name.ToString();
                    WaitingForMateria = true;
                    retrieveApproved = retrievalSent = false;
                    Status = $"Materia found on {MateriaItemName}. Choose Retrieve materia or Skip item.";
                    return;
                }
                if (!EnsureBroth(inventory, now)) return;
                var agent = AgentSalvage.Instance();
                if (agent == null) { Stop("Desynthesis agent unavailable."); return; }
                pending = (type, i, id, slot->GetQuantity()); deadline = now.AddSeconds(15);
                agent->SalvageItem(slot);
                AtkValue result = new();
                AtkValue* args = stackalloc AtkValue[2];
                args[0] = new AtkValue { Type = AtkValueType.Int, Int = 0 };
                args[1] = new AtkValue { Type = AtkValueType.Bool, Byte = 1 };
                agent->AgentInterface.ReceiveEvent(&result, args, 2, 1);
                Status = $"Desynthesizing {item.Name} · {completed} completed.";
                return;
            }
        }
        Stop($"Desynthesis complete: {completed} item(s). Protected, declined and ineligible items skipped.");
    }
}
