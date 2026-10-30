using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace RetainerPricer;

internal static unsafe class MateriaRetrieval
{
    public static bool TryBegin(IGameGui gui, IDataManager data, InventoryType bag, int index, InventoryItem* slot)
    {
        var existing = gui.GetAddonByName<AtkUnitBase>("MateriaRetrieveDialog");
        if (existing != null && existing->IsVisible) return false;
        uint label = data.GetExcelSheet<Addon>(ClientLanguage.English)
            .FirstOrDefault(row => row.Text.ToString().Equals("Retrieve Materia", StringComparison.OrdinalIgnoreCase)).RowId;
        var agent = AgentInventoryContext.Instance();
        if (label == 0 || agent == null) return false;
        agent->OpenForItemSlot(bag, index, 0, 0);
        if (agent->TargetInventoryId != bag || agent->TargetInventorySlotId != index || agent->TargetInventorySlot != slot
            || agent->ContextCallbackInfos == null || agent->ContextItemCount is < 1 or > 32) return false;
        for (int i=0; i<agent->ContextItemCount; i++)
        {
            var entry = agent->ContextCallbackInfos[i];
            if (entry.LabelId != label || entry.Handler == null || agent->IsContextItemDisabled(i)) continue;
            entry.Handler->HandleCallback((uint)index, bag, agent->TargetInventoryFlags, entry.CallbackParam);
            return true;
        }
        return false;
    }

    public static bool TryConfirm(IGameGui gui, InventoryType bag, int index, InventoryItem* slot)
    {
        var context = AgentInventoryContext.Instance();
        if (context == null || context->TargetInventoryId != bag || context->TargetInventorySlotId != index || context->TargetInventorySlot != slot) return false;
        var addon = gui.GetAddonByName<AtkUnitBase>("MateriaRetrieveDialog");
        if (addon == null || !addon->IsVisible) return false;
        // Native Begin button, also identified by ECommons' MateriaRetrieveDialog helper.
        var button = addon->GetComponentButtonById(17);
        if (button == null || !button->IsEnabled || button->OwnerNode == null || !button->OwnerNode->IsVisible()) return false;
        var ev = button->OwnerNode->AtkEventManager.Event;
        for (int n=0;ev!=null && n<64;n++,ev=ev->NextEvent)
        {
            if (ev->State.EventType != AtkEventType.ButtonClick || ev->Listener != addon) continue;
            AtkEvent copy = *ev;
            copy.NextEvent = null; copy.State.ReturnFlags = 0; copy.State.StateFlags = AtkEventStateFlags.None;
            AtkEventData eventData = default;
            ev->Listener->ReceiveEvent(AtkEventType.ButtonClick, (int)ev->Param, &copy, &eventData);
            return true;
        }
        return false;
    }
}
