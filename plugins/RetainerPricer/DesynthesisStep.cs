namespace RetainerPricer;

/// <summary>What the desynthesis step of a sequence needs from the game.</summary>
internal interface IDesynthesisHost
{
    /// <summary>True when something on the list can be desynthesized right now.</summary>
    bool HasWork { get; }
    bool PickerOpen { get; }
    bool AtBell { get; }
    void ClosePicker();
    bool TryUseBell();
    void Start();
    bool Running { get; }
    bool Succeeded { get; }
    string Status { get; }
    void Stop(string reason);
}

/// <summary>Desynthesis cannot run at the bell, so: leave it, desynthesize, then use the same bell again.</summary>
internal sealed class DesynthesisStep(IDesynthesisHost host)
{
    private enum Stage { Idle, LeaveBell, Working, Reopen }
    private Stage stage;
    private DateTimeOffset deadline, next;

    public bool IsRunning => stage != Stage.Idle;
    public bool Succeeded { get; private set; }
    public string Status { get; private set; } = "";
    public bool HasWork => host.HasWork;

    public void Begin(DateTimeOffset now)
    {
        Succeeded = false;
        stage = Stage.LeaveBell;
        deadline = now.AddSeconds(20);
        next = now;
        Status = "Leaving the bell to desynthesize...";
    }

    public void Update(DateTimeOffset now)
    {
        if (!IsRunning || now < next) return;
        next = now.AddSeconds(1);
        switch (stage)
        {
            case Stage.LeaveBell:
                if (host.PickerOpen) host.ClosePicker();
                else if (!host.AtBell)
                {
                    host.Start();
                    if (!host.Running && !host.Succeeded) { Fail($"Desynthesis did not start: {host.Status}"); return; }
                    stage = Stage.Working;
                    return;
                }
                if (now > deadline) Fail("The retainer list did not close for desynthesis.");
                break;
            case Stage.Working:
                if (host.Running) { Status = host.Status; return; }
                if (!host.Succeeded) { Fail(host.Status); return; }
                stage = Stage.Reopen;
                deadline = now.AddSeconds(30);
                next = now.AddSeconds(2);
                Status = "Desynthesis finished. Returning to the summoning bell...";
                break;
            case Stage.Reopen:
                if (host.PickerOpen) { stage = Stage.Idle; Succeeded = true; return; }
                if (now > deadline) { Fail("The summoning bell could not be reopened after desynthesis."); return; }
                if (!host.AtBell) host.TryUseBell();
                next = now.AddSeconds(3);
                break;
        }
    }

    public void Cancel(string reason)
    {
        if (!IsRunning) return;
        stage = Stage.Idle;
        if (host.Running) host.Stop(reason);
        Status = reason;
    }

    private void Fail(string reason) { stage = Stage.Idle; Succeeded = false; Status = reason; }
}