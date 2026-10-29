using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Item = Lumina.Excel.Sheets.Item;

namespace RetainerPricer;

internal sealed unsafe class DesynthesisController(PluginConfig config, NativeMarketBridge bridge,
    IDataManager data, ICondition condition, Func<bool> otherBusy)
{
    private static readonly InventoryType[] Bags = [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4];
    public bool IsRunning { get; private set; }
    public string Status { get; private set; } = "Ready.";
    private DateTimeOffset next, deadline;
    private (InventoryType Bag, int Slot, uint Id, uint Quantity)? pending;
    private int completed;
    public void Stop(string reason = "Desynthesis stopped.") { IsRunning = false; pending = null; Status = reason; }
    public void Start()
    {
        if (otherBusy()) { Status = "Wait for other Retainer Pricer operations to finish."; return; }
        if (bridge.IsClientStateUnavailable || !QuestManager.IsQuestComplete(65688))
        { Status = "Log in and unlock desynthesis first."; return; }
        if (config.DesynthesisItemIds.Count == 0) { Status = "Add items to the desynthesis list first."; return; }
        if (!ItemProtection.TryGetExcludedItemIds(bridge, config, out _, out _, out var error)) { Status = error; return; }
        completed = 0; pending = null; next = DateTimeOffset.MinValue;
        IsRunning = true; Status = "Desynthesis started. Close retainer and other interaction windows.";
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
                // Materia is valuable; leave melded items for explicit manual handling.
                bool melded = false;
                for (int m = 0; m < 5; m++) if (slot->Materia[m] != 0) melded = true;
                if (melded) continue;
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
        Stop($"Desynthesis complete: {completed} item(s). Protected, melded and ineligible items skipped.");
    }
}
