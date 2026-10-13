using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling;

namespace RetainerPricer;

internal sealed class InventoryListMenu : IDisposable
{
    private readonly IContextMenu menus;
    private readonly PluginConfig config;
    private readonly Action save;
    private readonly IReadOnlyDictionary<uint, (bool Marketable, bool Vendorable)> eligibility;
    private readonly NativeMarketBridge bridge;

    public InventoryListMenu(IContextMenu menus, PluginConfig config, Action save, NativeMarketBridge bridge,
        IReadOnlyDictionary<uint, (bool Marketable, bool Vendorable)> eligibility)
    {
        (this.menus, this.config, this.save) = (menus, config, save);
        this.eligibility = eligibility;
        this.bridge = bridge;
        menus.OnMenuOpened += OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (!config.ShowInventoryContextMenu) return;
        if (args.Target is not MenuTargetInventory target || target.TargetItem is not { } item) return;
        var id = item.BaseItemId;
        if (!eligibility.TryGetValue(id, out var capabilities)) return;
        var marketable = capabilities.Marketable && !item.IsCollectable && item.SpiritbondOrCollectability == 0;
        var vendorable = capabilities.Vendorable && !item.IsCollectable;
        var protectionsAvailable = ItemProtection.TryGetExcludedItemIds(bridge, config, out var protectedIds, out _, out _);
        var eligibleForSale = protectionsAvailable && !protectedIds.Contains(id);
        AddList(args, id, "exceptions", config.ExcludedItemIds, marketable || vendorable);
        AddList(args, id, "don't reprice", config.NoRepriceItemIds, marketable && eligibleForSale);
        var removeBatch = config.BatchSaleQuantities.ContainsKey(id);
        if (removeBatch || (marketable && eligibleForSale)) args.AddMenuItem(new MenuItem
        {
            Name = ColoredName(removeBatch, "batch selling"),
            OnClicked = _ =>
            {
                if (removeBatch)
                {
                    config.BatchSaleQuantities.Remove(id);
                    config.BatchSaleMaxQuantities.Remove(id);
                }
                else config.BatchSaleQuantities.TryAdd(id, 10);
                save();
            }
        });
        AddList(args, id, "retainer sale whitelist", config.RetainerSaleWhitelistItemIds,
            vendorable && eligibleForSale && !config.DontVendorItemIds.Contains(id));
        AddList(args, id, "don't vendor", config.DontVendorItemIds, vendorable);
    }

    private void AddList(IMenuOpenedArgs args, uint id, string name, List<uint> list, bool canAdd)
    {
        var remove = list.Contains(id);
        if (!remove && !canAdd) return;
        args.AddMenuItem(new MenuItem
        {
            Name = ColoredName(remove, name),
            OnClicked = _ =>
            {
                if (remove) list.RemoveAll(value => value == id);
                else if (!list.Contains(id)) list.Add(id);
                save();
            }
        });
    }

    private static SeString ColoredName(bool remove, string list) => new SeStringBuilder()
        .AddUiForeground((ushort)(remove ? 17 : 45))
        .AddText($"{(remove ? "Remove from" : "Add to")} {list}")
        .AddUiForegroundOff().Build();

    public void Dispose() => menus.OnMenuOpened -= OnMenuOpened;
}
