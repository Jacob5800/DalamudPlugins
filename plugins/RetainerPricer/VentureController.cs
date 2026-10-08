namespace RetainerPricer;

/// <summary>
/// Drives the game's own retainer UI for a user-started venture cycle. This controller has no
/// AutoRetainer reference, IPC, or runtime dependency.
/// </summary>
internal sealed class VentureController(NativeMarketBridge bridge, PluginConfig config)
{
    private enum Step
    {
        Idle,
        SelectRetainer,
        WaitForRetainerMenu,
        WaitForVentureSelectionMenu,
        WaitForVentureTaskList,
        WaitForResult,
        WaitForTaskAsk,
        WaitForPostAction,
        WaitForPicker
    }

    private static readonly TimeSpan ScreenTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ActionDelay = TimeSpan.FromMilliseconds(700);
    private readonly NativeMarketBridge bridge = bridge;
    private readonly PluginConfig config = config;
    private readonly List<RetainerIdentity> retainers = [];
    private readonly Dictionary<(ulong RetainerId, byte Level, RetainerVentureJob Job), IReadOnlyList<RetainerVentureOption>> ventureOptionCache = [];
    private DateTimeOffset ventureOptionCacheRefreshedAt;
    private Step step;
    private RetainerVentureMenuLabels? labels;
    private RetainerIdentity? currentRetainer;
    private ulong contentId;
    private uint worldId;
    private ulong previouslySelectedRetainerId;
    private int retainerIndex;
    private int dialogueClicks;
    private int processed;
    private int skipped;
    private DateTimeOffset nextActionAt;
    private DateTimeOffset deadline;
    private bool actionReassigns;
    private uint selectedTaskId;

    public bool IsRunning => step != Step.Idle;
    public string Status { get; private set; } = "Open the retainer picker to start a venture cycle.";
    public string VentureOptionsStatus { get; private set; } = "Venture options are generated automatically from each retainer's job and level.";

    public bool TryGetRetainerRoster(out IReadOnlyList<RetainerIdentity> roster)
        => bridge.TryGetOwnRetainers(out roster);

    public IReadOnlyList<RetainerVentureOption> GetVentureOptions(RetainerIdentity retainer, RetainerVentureJob job)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - ventureOptionCacheRefreshedAt >= TimeSpan.FromMinutes(1))
        {
            bridge.RefreshVentureGatheringLogCache();
            ventureOptionCache.Clear();
            ventureOptionCacheRefreshedAt = now;
        }
        var key = (retainer.RetainerId, retainer.Level, job);
        if (ventureOptionCache.TryGetValue(key, out var cached)) return cached;
        var options = bridge.GetRetainerVentureOptions(retainer.Level, job);
        ventureOptionCache[key] = options;
        return options;
    }

    public bool RefreshVentureOptions(out string message)
    {
        try
        {
            if (!bridge.TryGetOwnRetainers(out var roster))
            {
                message = "Open the retainer picker at a summoning bell so Retainer Pricer can read your retainer roster.";
                VentureOptionsStatus = message;
                return false;
            }

            bridge.RefreshVentureGatheringLogCache();
            ventureOptionCache.Clear();
            ventureOptionCacheRefreshedAt = DateTimeOffset.UtcNow;
            var availableCount = 0;
            foreach (var retainer in roster)
            {
                var job = config.RetainerVentureJobOverrides.TryGetValue(retainer.RetainerId, out var jobOverride)
                    ? jobOverride
                    : RetainerVentureJobExtensions.DetectFromClassJobId(retainer.ClassJobId);
                availableCount += GetVentureOptions(retainer, job).Count;
            }
            message = $"Refreshed venture options: {availableCount} available item ventures across {roster.Count} retainers. Gathering ventures require their items to be recorded in your Gathering Log.";
            VentureOptionsStatus = message;
            return true;
        }
        catch (Exception ex)
        {
            message = $"Couldn't refresh venture options: {ex.Message}";
            VentureOptionsStatus = message;
            return false;
        }
    }

    public void Start()
    {
        if (IsRunning) return;
        if (!config.RunVentures)
        { Status = "Enable Run ventures in the Ventures tab before starting the cycle."; return; }
        if (!bridge.IsRetainerPickerVisible)
        { Status = "Open the retainer picker at a summoning bell before starting a venture cycle."; return; }
        if (!bridge.TryGetCharacterContext(out contentId, out worldId, out var error))
        { Status = error; return; }
        if (!bridge.TryGetRetainerPickerOrder(out var pickerOrder, out error))
        { Status = error; return; }
        if (!bridge.TryGetRetainerVentureMenuLabels(out labels, out error))
        { Status = error; return; }
        if (pickerOrder.Count == 0)
        { Status = "No retainers were found in the open retainer picker."; return; }

        retainers.Clear();
        retainers.AddRange(pickerOrder);
        retainerIndex = 0;
        processed = 0;
        skipped = 0;
        currentRetainer = null;
        previouslySelectedRetainerId = 0;
        step = Step.SelectRetainer;
        nextActionAt = DateTimeOffset.UtcNow;
        Status = $"Venture cycle queued for {retainers.Count} retainer(s).";
    }

    public void Update()
    {
        if (!IsRunning) return;
        var now = DateTimeOffset.UtcNow;
        if (now < nextActionAt) return;
        if (bridge.IsClientStateUnavailable || bridge.IsCharacterOrWorldChanged(contentId, worldId))
        { Cancel("Venture cycle stopped because the character disconnected or changed world."); return; }

        // Talk may appear before or after any venture UI transition. Handle it once,
        // independently of the dialogue text or retainer personality.
        if (currentRetainer is { } speaking && bridge.IsRetainerDialogueVisible &&
            !bridge.IsRetainerPickerVisible)
        {
            if (now > deadline)
            { Cancel($"Venture cycle stopped because {speaking.Name}'s dialogue did not close."); return; }
            if (dialogueClicks < 4)
            {
                if (!bridge.TryAdvanceRetainerDialogue(speaking, previouslySelectedRetainerId, out var dialogueError))
                { Cancel($"Venture cycle stopped while advancing {speaking.Name}'s dialogue: {dialogueError}"); return; }
                dialogueClicks++;
                nextActionAt = now + ActionDelay;
                Status = $"Advancing {speaking.Name}'s dialogue ({dialogueClicks}/4)...";
            }
            return;
        }

        switch (step)
        {
            case Step.SelectRetainer:
                SelectNextRetainer(now);
                break;
            case Step.WaitForRetainerMenu:
                WaitForRetainerMenu(now);
                break;
            case Step.WaitForVentureSelectionMenu:
                WaitForVentureSelectionMenu(now);
                break;
            case Step.WaitForVentureTaskList:
                WaitForVentureTaskList(now);
                break;
            case Step.WaitForResult:
                if (bridge.IsVentureTaskResultVisible)
                {
                    if (!VerifyCurrentRetainer(out var identityError))
                    {
                        if (now > deadline) Cancel($"Venture cycle stopped before handling the report: {identityError}");
                        return;
                    }
                    if (!bridge.TryClickVentureResult(actionReassigns, out var error))
                    { Cancel($"Venture cycle stopped for {currentRetainer?.Name}: {error}"); return; }
                    processed++;
                    if (actionReassigns)
                        SetStep(Step.WaitForTaskAsk, now, $"Repeating {currentRetainer?.Name}'s completed venture...");
                    else
                        SetStep(Step.WaitForPostAction, now, $"Collecting {currentRetainer?.Name}'s completed venture...");
                }
                else if (now > deadline)
                    Cancel($"Venture cycle stopped because {currentRetainer?.Name}'s venture report did not open.");
                break;
            case Step.WaitForTaskAsk:
                if (bridge.IsVentureTaskAskVisible)
                {
                    if (!VerifyCurrentRetainer(out var identityError))
                    {
                        if (now > deadline) Cancel($"Venture cycle stopped before confirming the assignment: {identityError}");
                        return;
                    }
                    if (!bridge.TryClickVentureAssign(out var error))
                    { Cancel($"Venture cycle stopped for {currentRetainer?.Name}: {error}"); return; }
                    processed++;
                    SetStep(Step.WaitForPostAction, now, $"Assigning the selected venture to {currentRetainer?.Name}...");
                }
                else if (now > deadline)
                    Cancel($"Venture cycle stopped because {currentRetainer?.Name}'s assignment confirmation did not open.");
                break;
            case Step.WaitForPostAction:
                WaitForPostAction(now);
                break;
            case Step.WaitForPicker:
                if (bridge.IsRetainerPickerVisible)
                {
                    retainerIndex++;
                    currentRetainer = null;
                    SetStep(Step.SelectRetainer, now, $"Retainer {retainerIndex} of {retainers.Count} complete.");
                }
                else if (now > deadline)
                    Cancel($"Venture cycle stopped because the retainer picker did not return after {currentRetainer?.Name}.");
                break;
        }

        if (IsRunning && now > deadline && step is Step.WaitForRetainerMenu or Step.WaitForVentureSelectionMenu or Step.WaitForVentureTaskList)
            Cancel($"Venture cycle stopped because the expected retainer menu did not appear for {currentRetainer?.Name}.");
    }

    public void Cancel(string? message = null)
    {
        if (!IsRunning && message is null) return;
        step = Step.Idle;
        currentRetainer = null;
        Status = message ?? "Venture cycle stopped. The current game window was left open.";
    }

    private void SelectNextRetainer(DateTimeOffset now)
    {
        if (retainerIndex >= retainers.Count)
        {
            step = Step.Idle;
            currentRetainer = null;
            Status = $"Venture cycle complete: {processed} venture action(s), {skipped} retainer(s) skipped.";
            return;
        }
        if (!bridge.IsRetainerPickerVisible)
        { Cancel("Venture cycle stopped because the retainer picker closed unexpectedly."); return; }

        var target = retainers[retainerIndex];
        bridge.TryGetSelectedRetainerId(out previouslySelectedRetainerId);
        if (!bridge.TrySelectRetainerById(target, out var unavailable, out var error))
        {
            if (unavailable)
            {
                skipped++;
                retainerIndex++;
                Status = $"Skipped unavailable retainer {target.Name}.";
                return;
            }
            Cancel($"Venture cycle stopped before selecting {target.Name}: {error}");
            return;
        }
        currentRetainer = target;
        dialogueClicks = 0;
        SetStep(Step.WaitForRetainerMenu, now, $"Opening {target.Name}...");
    }

    private void WaitForRetainerMenu(DateTimeOffset now)
    {
        if (currentRetainer is not { } expected)
        { Cancel("Venture cycle lost its current retainer identity."); return; }
        if (bridge.IsRetainerMenuVisible)
        {
            if (!bridge.TryGetActiveRetainerVenture(expected, out var ventureId, out var error))
            {
                if (now <= deadline) return;
                Cancel($"Venture cycle stopped because {expected.Name} could not be verified: {error}");
                return;
            }
            InspectRetainerMenu(expected, ventureId, now);
            return;
        }
        if (bridge.IsRetainerPickerVisible)
        {
            skipped++;
            retainerIndex++;
            currentRetainer = null;
            SetStep(Step.SelectRetainer, now, $"Skipped {expected.Name}; its retainer menu did not open.");
        }
    }

    private void InspectRetainerMenu(RetainerIdentity expected, ushort ventureId, DateTimeOffset now)
    {
        if (labels is null)
        { Cancel("Venture cycle stopped because its localized game menu labels are unavailable."); return; }
        if (!bridge.TryRetainerMenuContains(labels.ViewReport, out var hasReport, out var error))
        { Cancel($"Venture cycle stopped while reading {expected.Name}'s menu: {error}"); return; }
        if (hasReport)
        {
            if (ventureId == 0)
            { Cancel($"Venture cycle stopped because {expected.Name}'s menu and venture data disagree."); return; }
            if (!bridge.TrySelectRetainerMenuEntry(text => string.Equals(text, labels.ViewReport, StringComparison.Ordinal), out _, out error))
            { Cancel($"Venture cycle stopped before opening {expected.Name}'s venture report: {error}"); return; }
            actionReassigns = config.RepeatCompletedVentures;
            SetStep(Step.WaitForResult, now, $"Opening {expected.Name}'s completed venture...");
            return;
        }

        if (ventureId == 0 && config.AssignQuickExplorationWhenIdle)
        {
            selectedTaskId = config.RetainerVentureTaskOverrides.TryGetValue(expected.RetainerId, out var configuredTaskId)
                ? configuredTaskId
                : RetainerVentureIds.QuickExploration;
            selectedTaskListCategoryOpened = false;
            var selectedJob = config.RetainerVentureJobOverrides.TryGetValue(expected.RetainerId, out var jobOverride)
                ? jobOverride
                : RetainerVentureJobExtensions.DetectFromClassJobId(expected.ClassJobId);
            if (selectedTaskId != RetainerVentureIds.QuickExploration &&
                !GetVentureOptions(expected, selectedJob).Any(option => option.TaskId == selectedTaskId))
            { Cancel($"The selected venture for {expected.Name} is not available for its job, level, or Gathering Log."); return; }
            if (!bridge.TrySelectRetainerMenuEntry(text => labels.AssignOptions.Contains(text, StringComparer.Ordinal), out _, out error))
            { Cancel($"Venture cycle stopped before assigning a venture to {expected.Name}: {error}"); return; }
            SetStep(Step.WaitForVentureSelectionMenu, now, $"Choosing a venture for {expected.Name}...");
            return;
        }

        SelectQuit(expected, now, ventureId == 0
            ? $"Leaving {expected.Name} idle."
            : $"Leaving {expected.Name}'s venture in progress.");
    }

    private void WaitForVentureSelectionMenu(DateTimeOffset now)
    {
        if (currentRetainer is not { } expected || labels is null)
        { Cancel("Venture cycle lost its retainer or menu state while choosing a venture."); return; }
        if (bridge.IsRetainerMenuVisible)
        {
            if (!bridge.TryGetActiveRetainerVenture(expected, out var ventureId, out var error))
            {
                if (now <= deadline) return;
                Cancel($"Venture cycle stopped because {expected.Name} could not be verified: {error}");
                return;
            }
            if (ventureId != 0)
            { Cancel($"Venture cycle stopped because {expected.Name} already has a venture while the assignment menu is open."); return; }
            var category = labels.QuickExploration;
            if (selectedTaskId != RetainerVentureIds.QuickExploration &&
                !bridge.TryGetRetainerVentureCategory(selectedTaskId, labels, out category, out error))
            { Cancel($"Venture cycle stopped before choosing a venture for {expected.Name}: {error}"); return; }
            if (!bridge.TrySelectRetainerMenuEntry(text => string.Equals(text, category, StringComparison.Ordinal), out _, out error))
            {
                if (now <= deadline) return;
                Cancel($"Venture cycle stopped before choosing {category} for {expected.Name}: {error}");
                return;
            }
            if (selectedTaskId == RetainerVentureIds.QuickExploration)
                SetStep(Step.WaitForTaskAsk, now, $"Confirming Quick Exploration for {expected.Name}...");
            else
                SetStep(Step.WaitForVentureTaskList, now, $"Selecting the configured venture for {expected.Name}...");
            return;
        }
        if (bridge.IsVentureTaskAskVisible)
        { Cancel($"Venture cycle stopped because {expected.Name}'s assignment screen opened before the selected venture could be verified."); return; }
    }

    private bool selectedTaskListCategoryOpened;

    private void WaitForVentureTaskList(DateTimeOffset now)
    {
        if (currentRetainer is not { } expected)
        { Cancel("Venture cycle lost its retainer while opening the venture item list."); return; }
        if (bridge.IsVentureTaskAskVisible)
        { Cancel($"Venture cycle stopped because {expected.Name}'s item venture list was skipped before the selected venture could be checked."); return; }
        if (!bridge.IsVentureTaskSupplyVisible) return;
        if (!bridge.TryGetActiveRetainerVenture(expected, out var ventureId, out var error))
        {
            if (now > deadline) Cancel($"Venture cycle stopped because {expected.Name} could not be verified: {error}");
            return;
        }
        if (ventureId != 0)
        { Cancel($"Venture cycle stopped because {expected.Name} already has a venture while the assignment list is open."); return; }
        if (bridge.TrySelectRetainerVentureTask(selectedTaskId, !selectedTaskListCategoryOpened,
                out var changedLevelGroup, out error))
        {
            SetStep(Step.WaitForTaskAsk, now, $"Confirming {selectedTaskId} for {expected.Name}...");
            return;
        }
        if (changedLevelGroup)
        {
            selectedTaskListCategoryOpened = true;
            nextActionAt = now + ActionDelay;
            Status = $"Opening the level range for {expected.Name}'s selected venture...";
            return;
        }
        if (now > deadline)
            Cancel($"Venture cycle stopped because the selected venture is not currently available for {expected.Name}: {error}");
        else if (!selectedTaskListCategoryOpened)
            Status = $"Waiting for the selected venture to appear in {expected.Name}'s item list...";
    }

    private void WaitForPostAction(DateTimeOffset now)
    {
        if (currentRetainer is not { } expected)
        { Cancel("Venture cycle lost its current retainer after a venture action."); return; }
        if (bridge.IsVentureTaskResultVisible || bridge.IsVentureTaskAskVisible)
        {
            if (!VerifyCurrentRetainer(out var identityError) && now > deadline)
            { Cancel($"Venture cycle stopped because the active retainer changed: {identityError}"); return; }
            if (now > deadline)
                Cancel($"Venture cycle stopped because {expected.Name}'s venture window did not close after the action.");
            return;
        }
        if (bridge.IsRetainerMenuVisible)
        {
            if (!bridge.TryGetActiveRetainerVenture(expected, out var ventureId, out var error))
            {
                if (now <= deadline) return;
                Cancel($"Venture cycle stopped because {expected.Name} could not be verified after the venture action: {error}");
                return;
            }
            InspectRetainerMenu(expected, ventureId, now);
            return;
        }
        if (bridge.IsRetainerPickerVisible)
        {
            retainerIndex++;
            currentRetainer = null;
            SetStep(Step.SelectRetainer, now, $"Completed {expected.Name}; moving to the next retainer.");
            return;
        }
        if (now > deadline)
            Cancel($"Venture cycle stopped because {expected.Name}'s retainer menu did not return after the action.");
    }

    private void SelectQuit(RetainerIdentity expected, DateTimeOffset now, string status)
    {
        if (labels is null)
        { Cancel($"Venture cycle stopped before leaving {expected.Name}'s menu: localized menu labels are unavailable."); return; }
        if (!bridge.TrySelectRetainerMenuEntry(
                text => string.Equals(text, labels.Quit, StringComparison.Ordinal), out _, out var error))
        { Cancel($"Venture cycle stopped before leaving {expected.Name}'s menu: {error}"); return; }
        dialogueClicks = 0;
        previouslySelectedRetainerId = expected.RetainerId;
        SetStep(Step.WaitForPicker, now, status);
    }

    private bool VerifyCurrentRetainer(out string error)
    {
        if (currentRetainer is not { } expected)
        { error = "The queued retainer identity is unavailable."; return false; }
        if (bridge.TryGetActiveRetainerVenture(expected, out _, out error)) return true;
        return false;
    }

    private void SetStep(Step next, DateTimeOffset now, string status)
    {
        dialogueClicks = 0;
        step = next;
        nextActionAt = now + ActionDelay;
        deadline = now + ScreenTimeout;
        Status = status;
    }
}
