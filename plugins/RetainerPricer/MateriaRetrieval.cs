using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace RetainerPricer;

internal static unsafe class MateriaRetrieval
{
    public static bool TryBegin(IGameGui gui, IDataManager data, InventoryType bag, int index, InventoryItem* slot, out string error)
    {
        error = "Materia retrieval dialog is already open. Close it and retry.";
        var existing = gui.GetAddonByName<AtkUnitBase>("MateriaRetrieveDialog");
        if (existing != null && existing->IsVisible) return false;
        uint label = data.GetExcelSheet<Addon>(ClientLanguage.English)
            .FirstOrDefault(row => row.Text.ToString().Equals("Retrieve Materia", StringComparison.OrdinalIgnoreCase)).RowId;
        var agent = AgentInventoryContext.Instance();
        error = "Retrieve Materia menu label or inventory agent is unavailable.";
        if (label == 0 || agent == null) return false;
        string expected = data.GetExcelSheet<Addon>().GetRow(label).Text.ToString();
        agent->OpenForItemSlot(bag, index, 0, 0);
        error = "Inventory context target changed before materia retrieval.";
        if (agent->TargetInventoryId != bag || agent->TargetInventorySlotId != index || agent->TargetInventorySlot != slot) return false;
        var menu = gui.GetAddonByName<AtkUnitBase>("ContextMenu");
        error = "Inventory context menu did not open for materia retrieval.";
        if (menu == null || !menu->IsVisible || menu->AtkValues == null || menu->AtkValuesCount < 9) return false;
        int count = (int)menu->AtkValues[0].UInt;
        if (count < 1 || count > 64 || menu->AtkValuesCount < count + 8) return false;
        var list = menu->GetComponentListById(2);
        error = "Retrieve Materia is missing or disabled in the item's context menu.";
        if (list == null) return false;
        // Native context-menu layout and callback: ECommons AddonMaster.ContextMenu.
        for (int i=0; i<count; i++)
        {
            var value = menu->AtkValues[i+8];
            if (value.Type is not (AtkValueType.String or AtkValueType.ManagedString) || value.String.Value == null) continue;
            if (!SeString.Parse(value.String.Value).TextValue.Equals(expected, StringComparison.Ordinal)) continue;
            var renderer = list->GetItemRenderer(i);
            if (renderer == null || !renderer->IsEnabled) return false;
            AtkValue* args = stackalloc AtkValue[3];
            args[0] = new AtkValue { Type = AtkValueType.Int, Int = 0 };
            args[1] = new AtkValue { Type = AtkValueType.Int, Int = i };
            args[2] = new AtkValue { Type = AtkValueType.Int, Int = 0 };
            menu->FireCallback(3, args, true);
            error = "";
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
