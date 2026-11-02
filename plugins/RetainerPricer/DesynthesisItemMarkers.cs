using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RetainerPricer;

internal sealed unsafe class DesynthesisItemMarkers(IGameGui gui, PluginConfig config)
{
    internal void Draw()
    {
        if (!config.MarkDesynthesisItems || config.DesynthesisItemIds.Count == 0) return;
        var order = ItemOrderModule.Instance();
        var inventory = InventoryManager.Instance();
        if (order == null || inventory == null) return;
        var listed = config.DesynthesisItemIds.ToHashSet();
        var expanded = gui.GetAddonByName<AddonInventoryExpansion>("InventoryExpansion");
        var large = gui.GetAddonByName<AddonInventoryLarge>("InventoryLarge");
        var normal = gui.GetAddonByName<AddonInventory>("Inventory");
        if (expanded != null && expanded->IsVisible && expanded->IsReady)
        {
            for (var page = 0; page < 4; page++) Grid($"InventoryGrid{page}E", page);
        }
        else if (large != null && large->IsVisible && large->IsReady && large->TabIndex is >= 0 and < 2)
        {
            Grid("InventoryGrid0", large->TabIndex * 2);
            Grid("InventoryGrid1", large->TabIndex * 2 + 1);
        }
        else if (normal != null && normal->IsVisible && normal->IsReady && normal->TabIndex is >= 0 and < 4)
            Grid("InventoryGrid", normal->TabIndex);

        var saddle = gui.GetAddonByName<AddonInventoryBuddy>("InventoryBuddy");
        if (saddle != null && saddle->IsVisible && saddle->IsReady && saddle->TabIndex is >= 0 and < 2)
        {
            var sorter = saddle->TabIndex == 0 ? order->SaddleBagSorter : order->PremiumSaddleBagSorter;
            for (var i = 0; i < 70; i++)
                Paint(sorter, i, saddle->Slots[i].Value, saddle->Scale, inventory, listed);
        }
        return;

        void Grid(string name, int page)
        {
            var grid = gui.GetAddonByName<AddonInventoryGrid>(name);
            if (grid == null || !grid->IsVisible || !grid->IsReady) return;
            for (var slot = 0; slot < 35; slot++)
                Paint(order->InventorySorter, page * 35 + slot, grid->Slots[slot].Value, grid->Scale, inventory, listed);
        }
    }

    private static void Paint(ItemOrderModuleSorter* sorter, int index, AtkComponentDragDrop* slot,
        float scale, InventoryManager* inventory, HashSet<uint> listed)
    {
        if (sorter == null || sorter->SortFunctionIndex != -1 || index >= sorter->Items.LongCount ||
            slot == null || slot->OwnerNode == null || !slot->OwnerNode->IsVisible()) return;
        var entry = sorter->Items[index].Value;
        if (entry == null) return;
        var container = inventory->GetInventoryContainer(sorter->InventoryType + entry->Page);
        if (container == null || !container->IsLoaded || entry->Slot >= container->Size) return;
        var item = container->GetInventorySlot(entry->Slot);
        if (item == null || item->ItemId == 0 || !listed.Contains(item->GetBaseItemId())) return;
        var node = slot->OwnerNode;
        var pos = new Vector2(node->ScreenX, node->ScreenY);
        ImGui.GetForegroundDrawList().AddRectFilled(pos, pos + new Vector2(node->Width, node->Height) * scale, 0x70FF9933u, 3);
    }
}
