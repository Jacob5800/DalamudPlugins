using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling;

namespace RetainerPricer;

internal sealed class InventoryListMenu : IDisposable
{
    private readonly IContextMenu menus;
    private readonly PluginConfig config;
    private readonly Action save;
    private readonly HashSet<uint> itemIds;

    public InventoryListMenu(IContextMenu menus, PluginConfig config, Action save, IEnumerable<uint> itemIds)
    {
        (this.menus, this.config, this.save) = (menus, config, save);
        this.itemIds = itemIds.ToHashSet();
        menus.OnMenuOpened += OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (!config.ShowInventoryContextMenu) return;
        if (args.Target is not MenuTargetInventory target || target.TargetItem is not { } item) return;
        var id = item.BaseItemId;
        if (!itemIds.Contains(id)) return;
        AddList(args, id, "exceptions", config.ExcludedItemIds);
        AddList(args, id, "don't reprice", config.NoRepriceItemIds);
        var removeBatch = config.BatchSaleQuantities.ContainsKey(id);
        args.AddMenuItem(new MenuItem
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
        AddList(args, id, "retainer sale whitelist", config.RetainerSaleWhitelistItemIds);
        AddList(args, id, "don't vendor", config.DontVendorItemIds);
    }

    private void AddList(IMenuOpenedArgs args, uint id, string name, List<uint> list)
    {
        var remove = list.Contains(id);
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
