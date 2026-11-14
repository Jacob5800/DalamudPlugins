namespace RetainerPricer;

/// <summary>Owns the full bell workflow; advances only after an explicit successful pass.</summary>
internal sealed class AutomationController(NativeMarketBridge bridge, PricingController pricing,
    VentureController ventures, AutoVendorController vendor, PluginConfig config, Func<bool> autoRetainerLoaded)
{
    private enum Stage { Idle, Collect, Assign, List, Reprice }
    private Stage stage;
    private bool collectEnabled, repriceEnabled, listEnabled, assignEnabled;
    private ulong contentId;
    private uint worldId;
    public AutomationRunSummary? LastSummary { get; private set; }
    public int CompletedRuns { get; private set; }
    public bool IsRunning => stage != Stage.Idle;
    public bool AutoRetainerLoaded => autoRetainerLoaded();
    public string Status { get; private set; } = "Full sequence is idle.";

    public void Start() => Start(allowEmptyExceptions: false);

    public void StartConfigured() => Start(false, configured: true);
    public void Start(bool allowEmptyExceptions) => Start(allowEmptyExceptions, configured: false);
    private void Start(bool allowEmptyExceptions, bool configured)
    {
        if (IsRunning || pricing.Busy || ventures.IsRunning || vendor.IsRunning) return;
        collectEnabled = !configured || config.AutoRunFullSequence || config.AutoRunVentures;
        repriceEnabled = !configured || config.AutoRunFullSequence || config.AutoUpdateExistingListings;
        listEnabled = !configured || config.AutoRunFullSequence || config.AutoListNewItems;
        assignEnabled = !configured || config.AutoRunFullSequence || config.AutoReassignVentures;
        if (AutoRetainerLoaded)
        { Status = "Unload AutoRetainer before starting Retainer Pricer automation."; return; }
        if ((collectEnabled || assignEnabled) && !config.RunVentures)
        { Status = "Enable Run ventures in Ventures before starting the full sequence."; return; }
        if (listEnabled && !allowEmptyExceptions && config.ExcludedItemIds.Count == 0 && !config.DontShowEmptyExceptionsWarningAgain)
        { Status = "Review the empty Exceptions warning with Start listing items before running automation."; return; }
        if (!bridge.IsRetainerPickerVisible || bridge.IsRetainerMenuVisible || bridge.IsRetainerSellListVisible ||
            bridge.IsRetainerDialogueVisible || bridge.IsSellWindowVisible || bridge.IsComparisonVisible ||
            bridge.IsVentureTaskAskVisible || bridge.IsVentureTaskResultVisible || bridge.IsVentureTaskSupplyVisible)
        { Status = "Open the retainer picker before starting the full sequence."; return; }
        if (!bridge.TryGetCharacterContext(out contentId, out worldId, out var error))
        { Status = error; return; }
        LastSummary = new AutomationRunSummary();
        pricing.BeginAutomationPriceSession(LastSummary);
        stage = Stage.Idle;
        Advance();
    }

    private void Advance()
    {
        var previous = stage;
        if (previous < Stage.Collect && collectEnabled && !AllVenturesStillAway(DateTimeOffset.UtcNow))
        {
            if (assignEnabled)
            {
                LastSummary?.BeginPhase("Collect and reassign ventures");
                stage = Stage.Assign; ventures.CollectAndReassign();
                Status = "Collecting rewards and reassigning each retainer before moving on...";
            }
            else
            {
                LastSummary?.BeginPhase("Collect venture rewards");
                stage = Stage.Collect; ventures.CollectRewards(); Status = "Collecting venture rewards...";
            }
            if (!ventures.IsRunning && !ventures.LastRunSucceeded) Fail(ventures.Status);
            return;
        }
        if (previous < Stage.Collect && collectEnabled)
            LastSummary?.Skip("Collect phase skipped because all venture rewards are still away");
        if (previous < Stage.Assign && assignEnabled)
        {
            LastSummary?.BeginPhase("Reassign ventures");
            stage = Stage.Assign; ventures.ReassignVentures(); Status = "Reassigning ventures...";
            if (!ventures.IsRunning && !ventures.LastRunSucceeded) Fail(ventures.Status);
            return;
        }
        if (previous < Stage.List && listEnabled)
        {
            LastSummary?.BeginPhase("New listings");
            stage = Stage.List; pricing.ListNewItemsAllRetainers(); Status = "Listing eligible new inventory...";
            if (!pricing.Busy) Fail(pricing.Status);
            return;
        }
        if (previous < Stage.Reprice && repriceEnabled)
        {
            LastSummary?.BeginPhase("Update existing listings");
            stage = Stage.Reprice; pricing.AutoUpdateAllRetainers(); Status = "Updating existing listings...";
            if (!pricing.Busy) Fail(pricing.Status);
            return;
        }
        CompleteSummary("Completed", "Selected automation steps complete.");
        pricing.EndAutomationPriceSession();
        stage = Stage.Idle; CompletedRuns++; Status = "Selected automation steps complete.";
    }

    private bool AllVenturesStillAway(DateTimeOffset now)
    {
        if (!bridge.TryGetOwnRetainers(out var retainers) || retainers.Count == 0) return false;
        return retainers.All(retainer => bridge.IsRetainerStillOnVenture(retainer, now));
    }

    public void Update()
    {
        if (!IsRunning) return;
        if (AutoRetainerLoaded || bridge.IsClientStateUnavailable || bridge.IsCharacterOrWorldChanged(contentId, worldId))
        { Cancel("Automation stopped: AutoRetainer loaded, disconnected, or character/world changed."); return; }
        switch (stage)
        {
            case Stage.Collect or Stage.Assign when !ventures.IsRunning:
                if (!ventures.LastRunSucceeded) { Fail(ventures.Status); return; }
                Advance();
                break;
            case Stage.Reprice or Stage.List when !pricing.Busy:
                if (!pricing.LastTraversalSucceeded) { Fail(pricing.Status); return; }
                Advance();
                break;
        }
    }

    private void CompleteSummary(string outcome, string detail)
    {
        LastSummary?.Complete(outcome, detail);
        if (LastSummary is { } summary)
            bridge.LogAutomationDiagnostic($"Automation summary: {outcome}, elapsed={summary.Elapsed.TotalSeconds:F1}s, checks={summary.PriceChecks}, cacheHits={summary.CacheHits}, listed={summary.Listed}, repriced={summary.Repriced}, alreadyPriced={summary.AlreadyPriced}, vendored={summary.Vendored}, phases=[{string.Join(", ", summary.Phases.Select(p => $"{p.Key}={p.Value.TotalSeconds:F1}s"))}], skipped=[{string.Join(", ", summary.SkipReasons.Select(p => $"{p.Key}={p.Value}"))}], detail={detail}");
    }

    private void Fail(string detail)
    {
        CompleteSummary("Stopped", $"{detail} {pricing.Progress}");
        pricing.EndAutomationPriceSession();
        stage = Stage.Idle;
        Status = $"Full sequence stopped: {detail}";
    }

    public void Cancel(string message = "Full sequence stopped. Submitted changes remain applied.")
    {
        if (IsRunning) CompleteSummary("Stopped", $"{message} {pricing.Progress}");
        stage = Stage.Idle;
        pricing.Cancel();
        ventures.Cancel();
        Status = message;
    }
}
