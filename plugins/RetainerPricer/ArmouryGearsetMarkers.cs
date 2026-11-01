using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace RetainerPricer;

internal sealed unsafe class ArmouryGearsetMarkers(IGameGui gui, NativeMarketBridge bridge, PluginConfig config)
{
    public void Draw()
    {
        if (!config.MarkArmouryGearsetItems) return;
        var addon = gui.GetAddonByName<AddonArmouryBoard>("ArmouryBoard");
        if (addon == null || !addon->IsVisible || addon->TabIndex is < 0 or >= 12) return;
        var order = ItemOrderModule.Instance();
        var inventory = InventoryManager.Instance();
        if (order == null || inventory == null || !bridge.TryReadSavedGearsetItemIds(out var ids, out _)) return;
        var sorter = order->ArmourySorter[addon->TabIndex].Value;
        if (sorter == null || sorter->SortFunctionIndex != -1) return;
        var count = Math.Min(50, (int)sorter->Items.LongCount);
        for (var i = 0; i < count; i++)
        {
            var entry = sorter->Items[i].Value;
            var component = addon->Slots[i].Value;
            if (entry == null || component == null || component->OwnerNode == null || !component->OwnerNode->IsVisible()) continue;
            var container = inventory->GetInventoryContainer(sorter->InventoryType + entry->Page);
            if (container == null || !container->IsLoaded || entry->Slot >= container->Size) continue;
            var item = container->GetInventorySlot(entry->Slot);
            if (item == null || item->ItemId == 0) continue;
            var raw = item->ItemId + ((item->Flags & InventoryItem.ItemFlags.HighQuality) != 0 ? 1000000u : 0u);
            if (!ids.Contains(raw)) continue;
            var node = component->OwnerNode;
            var pos = new Vector2(node->ScreenX, node->ScreenY);
            var size = new Vector2(node->Width * addon->Scale, node->Height * addon->Scale);
            ImGui.GetForegroundDrawList().AddRect(pos, pos + size, 0xFF3030FF, 3, ImDrawFlags.None, 2);
        }
    }
}
