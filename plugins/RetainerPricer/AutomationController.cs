namespace RetainerPricer;

/// <summary>Owns the full bell workflow; advances only after an explicit successful pass.</summary>
internal sealed class AutomationController(NativeMarketBridge bridge, PricingController pricing,
    VentureController ventures, AutoVendorController vendor, PluginConfig config, Func<bool> autoRetainerLoaded)
{
    private enum Stage { Idle, Collect, Reprice, List, Assign }
    private Stage stage;
    private ulong contentId;
    private uint worldId;
    public int CompletedRuns { get; private set; }
    public bool IsRunning => stage != Stage.Idle;
    public bool AutoRetainerLoaded => autoRetainerLoaded();
    public string Status { get; private set; } = "Full sequence is idle.";

    public void Start() => Start(allowEmptyExceptions: false);

    public void Start(bool allowEmptyExceptions)
    {
        if (IsRunning || pricing.Busy || ventures.IsRunning || vendor.IsRunning) return;
        if (AutoRetainerLoaded)
        { Status = "Unload AutoRetainer before starting Retainer Pricer automation."; return; }
        if (!config.RunVentures)
        { Status = "Enable Run ventures in Ventures before starting the full sequence."; return; }
        if (!allowEmptyExceptions && config.ExcludedItemIds.Count == 0 && !config.DontShowEmptyExceptionsWarningAgain)
        { Status = "Review the empty Exceptions warning with Start listing items before running automation."; return; }
        if (!bridge.IsRetainerPickerVisible || bridge.IsRetainerMenuVisible || bridge.IsRetainerSellListVisible ||
            bridge.IsRetainerDialogueVisible || bridge.IsSellWindowVisible || bridge.IsComparisonVisible ||
            bridge.IsVentureTaskAskVisible || bridge.IsVentureTaskResultVisible || bridge.IsVentureTaskSupplyVisible)
        { Status = "Open the retainer picker before starting the full sequence."; return; }
        if (!bridge.TryGetCharacterContext(out contentId, out worldId, out var error))
        { Status = error; return; }
        if (AllVenturesStillAway(DateTimeOffset.UtcNow))
        {
            stage = Stage.Reprice;
            pricing.AutoUpdateAllRetainers();
            Status = "All retainers are still on ventures; skipping reward collection and updating listings...";
            if (!pricing.Busy) Fail(pricing.Status);
            return;
        }
        stage = Stage.Collect;
        ventures.CollectRewards();
        Status = "Collecting venture rewards...";
        if (!ventures.IsRunning) Fail(ventures.Status);
    }

    private bool AllVenturesStillAway(DateTimeOffset now)
    {
        if (!bridge.TryGetOwnRetainers(out var retainers) || retainers.Count == 0) return false;
        return retainers.All(retainer => config.UniversalisRetainerListings.TryGetValue(retainer.RetainerId, out var cache)
            && cache.WorldId == worldId && cache.RetainerId == retainer.RetainerId
            && cache.RefreshedAt <= now && now - cache.RefreshedAt < TimeSpan.FromHours(1)
            && cache.VentureId is > 0 && cache.VentureCompletesAt is { } end && end > now.AddMinutes(1));
    }

    public void Update()
    {
        if (!IsRunning) return;
        if (AutoRetainerLoaded || bridge.IsClientStateUnavailable || bridge.IsCharacterOrWorldChanged(contentId, worldId))
        { Cancel("Automation stopped: AutoRetainer loaded, disconnected, or character/world changed."); return; }
        switch (stage)
        {
            case Stage.Collect when !ventures.IsRunning:
                if (!ventures.LastRunSucceeded) { Fail(ventures.Status); return; }
                stage = Stage.Reprice;
                pricing.AutoUpdateAllRetainers();
                Status = "Updating existing listings...";
                if (!pricing.Busy) Fail(pricing.Status);
                break;
            case Stage.Reprice when !pricing.Busy:
                if (!pricing.LastTraversalSucceeded) { Fail(pricing.Status); return; }
                stage = Stage.List;
                pricing.ListNewItemsAllRetainers();
                Status = "Listing eligible new inventory across retainers...";
                if (!pricing.Busy) Fail(pricing.Status);
                break;
            case Stage.List when !pricing.Busy:
                if (!pricing.LastTraversalSucceeded) { Fail(pricing.Status); return; }
                stage = Stage.Assign;
                ventures.ReassignVentures();
                Status = "Reassigning ventures...";
                if (!ventures.IsRunning) Fail(ventures.Status);
                break;
            case Stage.Assign when !ventures.IsRunning:
                if (!ventures.LastRunSucceeded) { Fail(ventures.Status); return; }
                stage = Stage.Idle;
                CompletedRuns++;
                Status = "Complete: rewards collected, listings updated, new items listed, ventures reassigned.";
                break;
        }
    }

    private void Fail(string detail)
    {
        stage = Stage.Idle;
        Status = $"Full sequence stopped: {detail}";
    }

    public void Cancel(string message = "Full sequence stopped. Submitted changes remain applied.")
    {
        stage = Stage.Idle;
        pricing.Cancel();
        ventures.Cancel();
        Status = message;
    }
}
