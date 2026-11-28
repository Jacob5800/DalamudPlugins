namespace RetainerPricer;

internal sealed class DesynthesisHost(DesynthesisController desynthesis, MultiCharacterHost bell) : IDesynthesisHost
{
    public bool HasWork => desynthesis.HasWork();
    public bool PickerOpen => bell.RetainerPickerOpen;
    public bool AtBell => bell.BellInteraction;
    public void ClosePicker() => bell.ClosePicker();
    public bool TryUseBell() => bell.TryUseBell();
    public void Start() => desynthesis.Start();
    public bool Running => desynthesis.IsRunning;
    public bool Succeeded => desynthesis.LastRunSucceeded;
    public string Status => desynthesis.Status;
    public void Stop(string reason) => desynthesis.Stop(reason);
}