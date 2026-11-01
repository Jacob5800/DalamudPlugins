using System;
using System.Collections.Generic;
using System.Linq;

namespace Workshoppa.Voyages;

internal enum VesselKind { Airship, Submarine }
internal sealed class VoyageOptions
{
    public float MarketTaxPercent { get; set; } = 5;
    public float MarketRunningCostPerHour { get; set; }
    public bool AutoRunOnPanelOpen { get; set; }
    public bool Airships { get; set; } = true;
    public bool Submarines { get; set; } = true;
    public bool Collect { get; set; } = true;
    public bool Redeploy { get; set; } = true;
    public bool ReturnNotifications { get; set; } = true;
    public bool Repair { get; set; }
    public Dictionary<string, VesselPlan> Vessels { get; set; } = new();
    public void Normalize()
    {
        Vessels = (Vessels ?? new()).Where(p=>p.Value!=null).ToDictionary(p=>p.Key,p=>p.Value);
        foreach(var plan in Vessels.Values){plan.Points ??= new();plan.BuildParts ??= new();}
    }
    public void CopyFrom(VoyageOptions source)
    {
        source.Normalize();
        MarketTaxPercent=source.MarketTaxPercent;MarketRunningCostPerHour=source.MarketRunningCostPerHour;
        AutoRunOnPanelOpen=source.AutoRunOnPanelOpen;Airships=source.Airships;Submarines=source.Submarines;
        Collect=source.Collect;Redeploy=source.Redeploy;Repair=source.Repair;ReturnNotifications=source.ReturnNotifications;
        Vessels=source.Vessels.ToDictionary(p=>p.Key,p=>p.Value.Copy());
    }
}
internal sealed class VesselPlan
{
    public bool Enabled { get; set; } = true;
    public bool UseCustomRoute { get; set; }
    public List<uint> Points { get; set; } = new();
    public bool AutoSwapBuild { get; set; }
    public List<uint> BuildParts { get; set; } = new();
    public VesselPlan Copy() => new() { Enabled = Enabled, UseCustomRoute = UseCustomRoute, Points = (Points ?? new()).ToList(), AutoSwapBuild = AutoSwapBuild, BuildParts = (BuildParts ?? new()).ToList() };
}
internal sealed record Vessel(VesselKind Kind, int Slot, string Name, long ReturnTime, uint Rank, ushort[] Conditions, bool PartsReady)
{
    public string Key(ulong house) => $"{house:X16}/{Kind}/{Slot}/{Name}";
}
internal sealed record Destination(uint Id, VesselKind Kind, uint Map, string Name, uint Rank);
internal static class VoyagePolicy
{
    internal static bool Eligible(Vessel vessel, VesselPlan plan, bool collect, bool redeploy, long now)
        => plan.Enabled && vessel.Name.Length > 0 && (vessel.ReturnTime == 0 ? redeploy : vessel.ReturnTime <= now && collect);
    internal static string? ValidateRoute(VesselPlan plan, VesselKind kind, IReadOnlyList<Destination> destinations, uint rank)
    {
        if (!plan.UseCustomRoute) return null;
        if (plan.Points.Count is < 1 or > 5 || plan.Points.Distinct().Count() != plan.Points.Count) return "Choose one to five different destinations.";
        var selected = plan.Points.Select(id => destinations.FirstOrDefault(d => d.Id == id && d.Kind == kind)).ToArray();
        if (selected.Any(d => d == null)) return "A configured destination no longer exists.";
        if (selected.Any(d => d!.Rank > rank)) return "A configured destination requires a higher vessel rank.";
        if (selected.Select(d => d!.Map).Distinct().Count() != 1) return "All destinations must be on the same map.";
        return null;
    }
    internal static bool TryRatio(string text, out uint required, out uint available)
    {
        required = available = 0;
        var parts = text.Split('/');
        return parts.Length == 2 && uint.TryParse(new string(parts[0].Where(char.IsAsciiDigit).ToArray()), out required)
            && uint.TryParse(new string(parts[1].Where(char.IsAsciiDigit).ToArray()), out available);
    }
}
internal interface IVoyageGame
{
    bool Available { get; }
    ulong Character { get; }
    ulong House { get; }
    bool Root { get; }
    bool AnyWindow { get; }
    bool Cutscene { get; }
    bool Result { get; }
    bool Detail { get; }
    bool Editor { get; }
    bool MapSelect { get; }
    bool RepairWindow { get; }
    bool VesselMenu { get; }
    IReadOnlyList<Destination> Destinations { get; }
    IReadOnlyList<Vessel> Roster(VesselKind kind);
    bool Picker(VesselKind kind);
    bool SelectedVesselMatches(Vessel vessel);
    uint SelectedMap { get; }
    bool SelectKind(VesselKind kind);
    bool SelectVessel(Vessel vessel);
    bool Collect();
    bool PreviousLog();
    bool Repeat(out string? error);
    bool Deploy(out string? error);
    bool QuitVessel();
    bool QuitPicker();
    bool OpenRepair(VesselKind kind);
    bool RequestRepair(Vessel vessel, int part, out string? error);
    bool ConfirmRepair(Vessel vessel, int part, out string? error);
    bool CloseRepair();
    bool EnsureBuild(Vessel vessel,IReadOnlyList<uint> parts,out string? error);
    void CancelBuild();
    bool OpenRoute();
    bool PickMap(uint map, out string? error);
    int SelectedCount(VesselKind kind);
    string RouteDistance { get; }
    bool PickPoint(Destination point, out string? error);
    bool ReviewRoute(out string? error);
}
