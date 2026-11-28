namespace RetainerPricer;

/// <summary>Game and plugin actions a multi-character run needs; kept narrow so the order of steps can be tested.</summary>
internal interface IMultiCharacterHost
{
    /// <summary>Zero while logged out.</summary>
    ulong ContentId { get; }
    bool CharacterReady { get; }
    /// <summary>Throws when Lifestream is not installed or enabled.</summary>
    bool LifestreamBusy { get; }
    /// <summary>Lifestream's error code: 0 accepted, 3 busy.</summary>
    int ChangeCharacter(string name, string world);
    string? BlockedReason { get; }
    bool RetainerPickerOpen { get; }
    bool BellInteraction { get; }
    /// <summary>False when no summoning bell is within reach.</summary>
    bool TryUseBell();
    bool TravelEnabled { get; }
    /// <summary>One step towards a city bell. Null while under way, empty once the bell was used, otherwise why it cannot be.</summary>
    string? Travel(DateTimeOffset now);
    /// <summary>What the trip is doing now.</summary>
    string TravelStatus { get; }
    /// <summary>Stops any walking this host started and forgets the trip.</summary>
    void StopTravel();
    void ClosePicker();
    void StartSequence();
    bool SequenceRunning { get; }
    int CompletedSequences { get; }
    string SequenceStatus { get; }
    void CancelSequence(string reason);
}

/// <summary>One pass over the ticked characters: log in, use the bell in reach, run the sequence, move on.</summary>
internal sealed class MultiCharacterRun(IMultiCharacterHost host)
{
    private enum Stage { Idle, Next, Switching, Settling, Travelling, OpeningBell, Running, Closing }

    private const int Accepted = 0, LifestreamIsBusy = 3;
    private static readonly TimeSpan SwitchLimit = TimeSpan.FromMinutes(15);

    private Stage stage;
    private readonly Queue<MultiCharacter> pending = new();
    private readonly List<string> skipped = [];
    private MultiCharacter current = new();
    private ulong previousId;
    private DateTimeOffset deadline, waitUntil;
    private DateTimeOffset? idleSince;
    private bool changeAccepted;
    private int completed, total, sequencesBefore;

    public bool IsRunning => stage != Stage.Idle;
    public string Status { get; private set; } = "Multi-character run is idle.";

    public void Start(IReadOnlyList<MultiCharacter> characters, DateTimeOffset now)
    {
        if (IsRunning) return;
        var chosen = characters.Where(character => character.Included).ToList();
        if (chosen.Count == 0) { Status = "Tick at least one character first."; return; }
        if (host.BlockedReason is { } blocked) { Status = blocked; return; }
        try
        {
            if (host.LifestreamBusy) { Status = "Lifestream is busy. Wait for it to finish first."; return; }
        }
        catch { Status = "Install and enable Lifestream before starting a multi-character run."; return; }

        pending.Clear();
        skipped.Clear();
        // Start with whoever is logged in, which avoids one needless relog.
        foreach (var character in chosen.OrderBy(character => character.ContentId == host.ContentId ? 0 : 1))
            pending.Enqueue(character);
        total = pending.Count;
        completed = 0;
        waitUntil = now;
        stage = Stage.Next;
        Status = $"Starting a run over {total} character(s)...";
    }

    public void Stop(string reason = "Multi-character run stopped.")
    {
        if (!IsRunning) return;
        stage = Stage.Idle;
        pending.Clear();
        host.StopTravel();
        if (host.SequenceRunning) host.CancelSequence(reason);
        Status = $"{reason} {Tally()}";
    }

    public void Update(DateTimeOffset now)
    {
        if (!IsRunning || now < waitUntil) return;
        waitUntil = now.AddMilliseconds(500);
        try { Tick(now); }
        catch (Exception ex) { Stop("Multi-character run stopped: " + ex.Message); }
    }

    private void Tick(DateTimeOffset now)
    {
        if (stage is Stage.Travelling or Stage.OpeningBell or Stage.Running or Stage.Closing && host.ContentId != current.ContentId)
        { Stop($"Stopped: {current.Label} is no longer logged in."); return; }

        switch (stage)
        {
            case Stage.Next:
                if (!pending.TryDequeue(out var next))
                { stage = Stage.Idle; Status = $"Multi-character run finished. {Tally()}"; return; }
                current = next;
                previousId = host.ContentId;
                changeAccepted = false;
                idleSince = null;
                deadline = now + SwitchLimit;
                stage = Stage.Switching;
                break;

            case Stage.Switching:
                Switch(now);
                break;

            case Stage.Settling:
                if (!host.CharacterReady || host.ContentId != current.ContentId)
                { Stop($"Stopped: {current.Label} did not stay logged in."); return; }
                if (host.BlockedReason is { } blocked) { Stop(blocked); return; }
                stage = Stage.OpeningBell;
                deadline = now.AddSeconds(20);
                if (host.RetainerPickerOpen) return;
                if (host.TryUseBell()) Status = $"Using the summoning bell on {current.Label}...";
                else if (host.TravelEnabled)
                {
                    host.StopTravel();
                    stage = Stage.Travelling;
                    deadline = now.AddMinutes(3);
                    Status = $"Taking {current.Label} to a summoning bell...";
                }
                else Skip("no summoning bell within reach");
                break;

            case Stage.Travelling:
                if (host.BlockedReason is { } interrupted) { Stop(interrupted); return; }
                var outcome = host.Travel(now);
                if (outcome is null && now <= deadline) { Status = $"Travelling on {current.Label}: {host.TravelStatus}"; return; }
                host.StopTravel();
                if (outcome is "") { stage = Stage.OpeningBell; deadline = now.AddSeconds(20); }
                else Skip(outcome ?? $"the trip to the summoning bell took too long while {host.TravelStatus}");
                break;

            case Stage.OpeningBell:
                if (host.RetainerPickerOpen)
                {
                    sequencesBefore = host.CompletedSequences;
                    host.StartSequence();
                    stage = Stage.Running;
                    Status = $"Running the sequence on {current.Label} ({completed + skipped.Count + 1}/{total})...";
                    waitUntil = now.AddSeconds(1);
                }
                else if (now > deadline) Skip("the retainer list did not open");
                break;

            case Stage.Running:
                if (host.SequenceRunning) return;
                if (host.CompletedSequences <= sequencesBefore)
                { Stop($"Stopped on {current.Label}: {host.SequenceStatus}"); return; }
                completed++;
                stage = Stage.Closing;
                deadline = now.AddSeconds(30);
                break;

            case Stage.Closing:
                if (host.RetainerPickerOpen) host.ClosePicker();
                else if (!host.BellInteraction) { stage = Stage.Next; return; }
                if (now > deadline) Stop($"Stopped: the retainer windows on {current.Label} did not close.");
                break;
        }
    }

    private void Switch(DateTimeOffset now)
    {
        var id = host.ContentId;
        if (id == current.ContentId)
        {
            if (host.CharacterReady)
            {
                stage = Stage.Settling;
                waitUntil = now.AddSeconds(5);
                Status = $"Logged in as {current.Label}. Waiting for the game to settle...";
            }
            else if (now > deadline) Stop($"Stopped: {current.Label} did not finish loading.");
            return;
        }
        if (id != 0 && id != previousId) { Stop($"Stopped: a different character than {current.Label} logged in."); return; }
        if (now > deadline) { Stop($"Stopped: logging in as {current.Label} took too long."); return; }

        var busy = host.LifestreamBusy;
        if (!changeAccepted)
        {
            var code = host.ChangeCharacter(current.Name, current.World);
            if (code == Accepted) { changeAccepted = true; Status = $"Switching to {current.Label}..."; }
            else if (code != LifestreamIsBusy) { Stop($"Stopped: Lifestream refused to switch to {current.Label} (code {code})."); return; }
            waitUntil = now.AddSeconds(5);
            return;
        }
        // Lifestream going quiet without the character arriving means the login failed or was cancelled.
        if (busy) { idleSince = null; return; }
        idleSince ??= now;
        if (now - idleSince.Value >= TimeSpan.FromSeconds(90))
            Stop($"Stopped: Lifestream finished without logging in as {current.Label}.");
    }

    private void Skip(string reason)
    {
        skipped.Add($"{current.Label} ({reason})");
        Status = $"Skipped {current.Label}: {reason}.";
        stage = Stage.Next;
    }

    private string Tally() => $"{completed} of {total} character(s) completed." +
        (skipped.Count > 0 ? $" Skipped: {string.Join("; ", skipped)}." : "");
}
