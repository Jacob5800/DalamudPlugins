using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace RetainerPricer;

internal sealed unsafe class ArmouryGearsetMarkers(IGameGui gui, NativeMarketBridge bridge, PluginConfig config, IReadOnlySet<uint> marketableIds)
{
    private readonly DesynthesisItemMarkers desynthesisMarkers = new(gui, config);
    public void Draw()
    {
        desynthesisMarkers.Draw();
        if (!config.MarkArmouryGearsetItems && !config.MarkMarketableArmouryItems && !config.MarkDesynthesisItems) return;
        var addon = gui.GetAddonByName<AddonArmouryBoard>("ArmouryBoard");
        if (addon == null || !addon->IsVisible || addon->TabIndex is < 0 or >= 12) return;
        var order = ItemOrderModule.Instance();
        var inventory = InventoryManager.Instance();
        if (order == null || inventory == null) return;
        bridge.TryReadSavedGearsetItemIds(out var ids, out _);
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
            bool saved = config.MarkArmouryGearsetItems && ids != null && ids.Contains(raw);
            bool marketable = config.MarkMarketableArmouryItems && marketableIds.Contains(item->GetBaseItemId())
                && item->GetSpiritbondOrCollectability() == 0 && !item->IsCollectable();
            bool desynthesis = config.MarkDesynthesisItems && config.DesynthesisItemIds.Contains(item->GetBaseItemId());
            if (!saved && !marketable && !desynthesis) continue;
            var node = component->OwnerNode;
            var pos = new Vector2(node->ScreenX, node->ScreenY);
            var size = new Vector2(node->Width * addon->Scale, node->Height * addon->Scale);
            ImGui.GetForegroundDrawList().AddRectFilled(pos, pos + size, saved ? 0x503030FFu : desynthesis ? 0x70FF9933u : 0x5030CC30u, 3);
            ImGui.GetForegroundDrawList().AddRect(pos, pos + size, saved ? 0xFF3030FFu : desynthesis ? 0xFFFF9933u : 0xFF30CC30u, 3, ImDrawFlags.None, 2);
        }
    }
}
