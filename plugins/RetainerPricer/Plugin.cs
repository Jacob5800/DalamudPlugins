using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Item = Lumina.Excel.Sheets.Item;
using World = Lumina.Excel.Sheets.World;

namespace RetainerPricer;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly IDtrBarEntry serverInfoBarEntry;
    private DateTimeOffset nextServerBarUpdate;
    private readonly WindowSystem windows = new("RetainerPricer");
    private readonly PluginConfig config;
    private readonly NativeMarketBridge bridge;
    private readonly UniversalisClient universalis = new();
    private readonly FeedbackClient feedback = new();
    private readonly PricingController controller;
    private readonly SniperMonitor sniper;
    private readonly AutoVendorController vendor;
    private readonly VentureController ventures;
    private readonly MainWindow window;
    private readonly InventoryListMenu inventoryMenus;
    private readonly UndercutNotifier undercuts;
    private readonly ICondition condition;
    private readonly IChatGui chat;
    private readonly ItemPriceTooltip itemPriceTooltip;
    private readonly MarketboardRoi marketboardRoi;
    private bool wasOpen;
    private bool disposed;
    private readonly bool rpAliasRegistered;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IFramework framework,
        IGameGui gameGui, IDataManager data, IPlayerState player, IClientState clientState,
        ICondition condition, IAddonLifecycle addons, IDtrBar dtrBar,
        IGameInteropProvider interop, ISigScanner sigScanner, IPluginLog log, IContextMenu contextMenu, IChatGui chat)
    {
        (this.pluginInterface, this.commands, this.framework, this.log) = (pluginInterface, commands, framework, log);
        (this.condition, this.chat) = (condition, chat);
        config = pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        var migrateConfig = config.Version < 14;
        if (config.Version < 8)
        {
            // Move users from the former 0.10 default while preserving any custom threshold.
            if (Math.Abs(config.SniperThresholdFraction - 0.10) < 0.000001)
                config.SniperThresholdFraction = 0.910;
        }
        if (migrateConfig) config.Version = 14;
        config.Normalize();
        if (migrateConfig) pluginInterface.SavePluginConfig(config);
        serverInfoBarEntry = dtrBar.Get("Retainer Pricer", "RP");
        serverInfoBarEntry.Tooltip = "Click to open Retainer Pricer";
        serverInfoBarEntry.OnClick = _ => Toggle();
        serverInfoBarEntry.Shown = config.ShowServerInfoBarButton;
        bridge = new NativeMarketBridge(gameGui, data, player, clientState, condition, addons, interop, sigScanner, log);
        marketboardRoi = new MarketboardRoi(gameGui, bridge, universalis, config);
        var itemSheet = data.GetExcelSheet<Item>();
        var itemChoices = itemSheet
            .Select(item => new ItemChoice(item.RowId, item.Name.ToString()))
            .Where(item => item.ItemId != 0 && !string.IsNullOrWhiteSpace(item.Name))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var marketableItemIds = itemSheet
            .Where(item => item.RowId != 0 && !item.IsUntradable && item.ItemSearchCategory.RowId != 0)
            .Select(item => item.RowId)
            .ToHashSet();
        itemPriceTooltip = new ItemPriceTooltip(gameGui, bridge, universalis, config, addons, marketableItemIds, data);
        var marketableItemChoices = itemChoices.Where(item => marketableItemIds.Contains(item.ItemId)).ToArray();
        var worldNames = data.GetExcelSheet<World>()
            .Where(world => world.RowId != 0)
            .GroupBy(world => world.RowId)
            .ToDictionary(group => group.Key, group => group.First().Name.ToString());
        controller = new PricingController(bridge, universalis, config, marketableItemIds, Save);
        universalis.DiagnosticLog = message => log.Warning("{Diagnostic}", message);
        sniper = new SniperMonitor(universalis, config, marketableItemChoices, worldNames);
        vendor = new AutoVendorController(bridge, universalis, config, marketableItemIds);
        ventures = new VentureController(bridge, config);
        undercuts = new UndercutNotifier(universalis, config);
        inventoryMenus = new InventoryListMenu(contextMenu, config, Save, bridge, itemSheet
            .Where(item => item.RowId != 0)
            .ToDictionary(item => item.RowId, item =>
                (!item.IsUntradable && item.ItemSearchCategory.RowId != 0, item.PriceLow > 0)));
        controller.AutoUpdateCompleted += StartVenturesAfterAutoUpdate;
        window = new MainWindow(config, controller, itemChoices, bridge.GetHomeWorld, Save, Dispatch,
            () => bridge.RetainerAvailabilityError, feedback, sniper, vendor,
            shown => serverInfoBarEntry.Shown = shown, ventures, pluginInterface.IsTesting, undercuts,
            UndercutsPaused);
        inventoryMenus.SearchUniversalis = window.SearchMarketboardItem;
        windows.AddWindow(window);
        if (bridge.LocalAvailabilityError is { } localCompatibilityError)
            log.Warning("Retainer Pricer local pricing: {Error}", localCompatibilityError);
        if (bridge.RetainerAvailabilityError is { } retainerCompatibilityError)
            log.Warning("Retainer Pricer: {Error}", retainerCompatibilityError);
        if (bridge.ItemSelectorAvailabilityError is { } selectorCompatibilityError)
            log.Warning("Retainer Pricer: {Error}", selectorCompatibilityError);
        window.MarketboardRoiDiagnostics = () => marketboardRoi.Diagnostics;
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.Draw += DrawMarketboardClipboard;
        pluginInterface.UiBuilder.Draw += DrawRetainerVentureButton;
        pluginInterface.UiBuilder.Draw += itemPriceTooltip.Draw;
        pluginInterface.UiBuilder.Draw += marketboardRoi.Draw;
        pluginInterface.UiBuilder.OpenMainUi += Toggle;
        pluginInterface.UiBuilder.OpenConfigUi += Toggle;
        framework.Update += Update;
        commands.AddHandler("/retainerpricer", new CommandInfo((_, _) => Toggle())
        { HelpMessage = "Open Retainer Pricer: automatically price new listings and review existing listing updates." });
        commands.AddHandler("/retainer", new CommandInfo((_, _) => Toggle())
        { HelpMessage = "Open Retainer Pricer." });
        rpAliasRegistered = commands.AddHandler("/rp", new CommandInfo((_, _) => Toggle())
        { HelpMessage = "Open Retainer Pricer." });
        if (!rpAliasRegistered) log.Warning("The /rp command is already registered by another plugin. Use /retainerpricer or /retainer.");
    }

    private void Dispatch(Action action)
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            if (disposed) return;
            try { action(); }
            catch (Exception ex)
            {
                log.Error(ex, "Retainer Pricer operation failed");
                controller.Cancel("The operation failed. No further prices will be submitted; see Dalamud's log.");
                vendor.Cancel("Retainer selling stopped after an unexpected error. Check the retainer inventory before continuing.");
                ventures.Cancel("Venture cycle stopped after an unexpected error. Check the current retainer window before continuing.");
            }
        });
    }

    private void Update(IFramework _)
    {
        if (disposed) return;
        try
        {
            controller.Update();
            if (controller.MateriaWarningItem is not null) window.IsOpen = true;
            vendor.Update();
            ventures.Update();
            undercuts.SetSniperWatching(sniper.WatchingMarketScope == config.UndercutMarketScope);
            undercuts.Update(bridge.GetHomeWorld(), UndercutsPaused(), row =>
            {
                var name = window.ItemName(row.Listing.ItemId);
                chat.Print(new XivChatEntry { Type = XivChatType.Echo,
                    Message = new SeStringBuilder().AddText($"[Retainer Pricer] Undercut: {name} ({(row.Listing.IsHq ? "HQ" : "NQ")}) on {row.Retainer}: your saved price {row.Listing.Price:N0} gil, competitor {row.LowestPrice:N0} gil.").Build() });
            });
            var scanning = sniper.IsInitialScanRunning || sniper.IsVerifyingCandidate || undercuts.Checking;
            if (DateTimeOffset.UtcNow >= nextServerBarUpdate)
            {
                window.PriceInsightLoaded = pluginInterface.InstalledPlugins.Any(plugin => plugin.IsLoaded &&
                    (string.Equals(plugin.InternalName.Replace(" ", ""), "PriceInsight", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(plugin.Name, "Price Insight", StringComparison.OrdinalIgnoreCase)));
                nextServerBarUpdate = DateTimeOffset.UtcNow.AddSeconds(1);
                var completed = bridge.GetCompletedVentureNames();
                var text = new SeStringBuilder();
                if (completed.Count > 0) text.AddUiForeground(45);
                text.AddText(scanning ? "RP (scanning…)" : "RP");
                if (completed.Count > 0) text.AddUiForegroundOff();
                serverInfoBarEntry.Text = text.Build();
                serverInfoBarEntry.Tooltip = (completed.Count > 0
                    ? $"Completed ventures on available retainers ({completed.Count}): {string.Join(", ", completed)}.\n"
                    : "No completed ventures on available retainers.\n")
                    + (scanning ? "Fetching Sniper history or checking undercuts.\n" : "")
                    + "Click to open Retainer Pricer. " + bridge.VentureReadinessStatus;
            }
            var open = controller.HasRetainer;
            if (config.OpenWithRetainer && open && !wasOpen) window.IsOpen = true;
            wasOpen = open;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Retainer Pricer stopped after an unexpected error");
            controller.Cancel("Pricing stopped after an unexpected error. Reopen the plugin to check its status.");
            vendor.Cancel("Retainer selling stopped after an unexpected error. Check the retainer inventory before continuing.");
            ventures.Cancel("Venture cycle stopped after an unexpected error. Check the current retainer window before continuing.");
        }
    }

    private void StartVenturesAfterAutoUpdate()
    {
        if (config.RunVentures && !disposed && !vendor.IsRunning)
            ventures.Start();
    }

    private string clipboardSearchMessage = "";
    private void DrawMarketboardClipboard()
    {
        if (disposed || !bridge.TryGetMarketboardMenuPosition(out var position)) return;
        Dalamud.Bindings.ImGui.ImGui.SetNextWindowPos(new System.Numerics.Vector2(position.X, Math.Max(0, position.Y - 62)), Dalamud.Bindings.ImGui.ImGuiCond.Always);
        Dalamud.Bindings.ImGui.ImGui.SetNextWindowBgAlpha(1);
        var flags = Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoDecoration | Dalamud.Bindings.ImGui.ImGuiWindowFlags.AlwaysAutoResize |
            Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoSavedSettings | Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoMove | Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoFocusOnAppearing;
        if (Dalamud.Bindings.ImGui.ImGui.Begin("Marketboard clipboard##RetainerPricer", flags))
        {
            if (Dalamud.Bindings.ImGui.ImGui.Button("Open buy list")) window.OpenBuyList();
            Dalamud.Bindings.ImGui.ImGui.BeginDisabled(!controller.CanSearchBuyListItem);
            if (Dalamud.Bindings.ImGui.ImGui.Button("Paste clipboard and search"))
            {
                var name = Dalamud.Bindings.ImGui.ImGui.GetClipboardText()?.Trim();
                var item = sniper.MarketableItems.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
                if (item is null) clipboardSearchMessage = "Copy an exact marketable item name first.";
                else { clipboardSearchMessage = ""; Dispatch(() => controller.SearchBuyListItem(item.ItemId)); }
            }
            Dalamud.Bindings.ImGui.ImGui.EndDisabled();
            if (!string.IsNullOrEmpty(clipboardSearchMessage)) Dalamud.Bindings.ImGui.ImGui.TextUnformatted(clipboardSearchMessage);
        }
        Dalamud.Bindings.ImGui.ImGui.End();
    }

    private void DrawRetainerVentureButton()
    {
        if (disposed || !bridge.TryGetRetainerVentureButtonPosition(out var position)) return;
        Dalamud.Bindings.ImGui.ImGui.SetNextWindowPos(position);
        var flags = Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoDecoration | Dalamud.Bindings.ImGui.ImGuiWindowFlags.AlwaysAutoResize |
            Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoSavedSettings | Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoMove |
            Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoBackground | Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoFocusOnAppearing;
        Dalamud.Bindings.ImGui.ImGui.PushStyleVar(Dalamud.Bindings.ImGui.ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);
        if (Dalamud.Bindings.ImGui.ImGui.Begin("Retainer venture shortcut##RetainerPricer", flags))
        {
            Dalamud.Bindings.ImGui.ImGui.BeginDisabled(!config.RunVentures || controller.Busy || vendor.IsRunning || ventures.IsRunning);
            Dalamud.Bindings.ImGui.ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Button, new System.Numerics.Vector4(0.84f, 0.70f, 0.12f, 1));
            Dalamud.Bindings.ImGui.ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.96f, 0.82f, 0.22f, 1));
            Dalamud.Bindings.ImGui.ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.70f, 0.57f, 0.08f, 1));
            Dalamud.Bindings.ImGui.ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Text, new System.Numerics.Vector4(0.08f, 0.07f, 0.02f, 1));
            if (Dalamud.Bindings.ImGui.ImGui.Button(ventures.IsRunning ? "Venture cycle running..." : "Start venture cycle")) Dispatch(ventures.Start);
            Dalamud.Bindings.ImGui.ImGui.PopStyleColor(4);
            Dalamud.Bindings.ImGui.ImGui.EndDisabled();
            if (Dalamud.Bindings.ImGui.ImGui.IsItemHovered(Dalamud.Bindings.ImGui.ImGuiHoveredFlags.AllowWhenDisabled))
                Dalamud.Bindings.ImGui.ImGui.SetTooltip(config.RunVentures ? "Start your configured Retainer Pricer venture cycle. Finish active operations first." : "Enable Run ventures in Retainer Pricer's Ventures tab first.");
        }
        Dalamud.Bindings.ImGui.ImGui.End();
        Dalamud.Bindings.ImGui.ImGui.PopStyleVar();
    }

    private void Toggle() => window.Toggle();
    private bool UndercutsPaused() => config.HideUndercutInDuties &&
        (condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95]);
    private void Save() => pluginInterface.SavePluginConfig(config);

    public void Dispose()
    {
        disposed = true;
        inventoryMenus.Dispose();
        undercuts.Dispose();
        controller.AutoUpdateCompleted -= StartVenturesAfterAutoUpdate;
        framework.Update -= Update;
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.Draw -= DrawMarketboardClipboard;
        pluginInterface.UiBuilder.Draw -= DrawRetainerVentureButton;
        pluginInterface.UiBuilder.Draw -= itemPriceTooltip.Draw;
        itemPriceTooltip.Dispose();
        pluginInterface.UiBuilder.Draw -= marketboardRoi.Draw;
        marketboardRoi.Dispose();
        pluginInterface.UiBuilder.OpenMainUi -= Toggle;
        pluginInterface.UiBuilder.OpenConfigUi -= Toggle;
        commands.RemoveHandler("/retainerpricer");
        commands.RemoveHandler("/retainer");
        if (rpAliasRegistered) commands.RemoveHandler("/rp");
        serverInfoBarEntry.Remove();
        controller.Dispose();
        vendor.Dispose();
        ventures.Cancel();
        sniper.Dispose();
        bridge.Dispose();
        universalis.Dispose();
        feedback.Dispose();
        windows.RemoveAllWindows();
    }
}
