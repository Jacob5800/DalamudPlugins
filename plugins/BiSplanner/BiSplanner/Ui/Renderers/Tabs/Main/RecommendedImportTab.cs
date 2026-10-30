using BiSplanner.Gear;
using BiSplanner.Import;
using BiSplanner.Services.Gearsets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;

namespace BiSplanner.Ui.Renderers.Tabs.Main;

public sealed class RecommendedImportTab(HttpClient http, IGearsetsService gearsets) : TabRenderer<MainWindowTab>
{
    private static readonly (string Job, string Role, string Slug, string IcyRole, string Abbreviation)[] Jobs = [
        ("Paladin", "tanks", "paladin", "tank", "PLD"), ("Warrior", "tanks", "warrior", "tank", "WAR"),
        ("Dark Knight", "tanks", "dark-knight", "tank", "DRK"), ("Gunbreaker", "tanks", "gunbreaker", "tank", "GNB"),
        ("White Mage", "healers", "white-mage", "healer", "WHM"), ("Scholar", "healers", "scholar", "healer", "SCH"),
        ("Astrologian", "healers", "astrologian", "healer", "AST"), ("Sage", "healers", "sage", "healer", "SGE"),
        ("Monk", "melee", "monk", "dps", "MNK"), ("Dragoon", "melee", "dragoon", "dps", "DRG"),
        ("Ninja", "melee", "ninja", "dps", "NIN"), ("Samurai", "melee", "samurai", "dps", "SAM"),
        ("Reaper", "melee", "reaper", "dps", "RPR"), ("Viper", "melee", "viper", "dps", "VPR"),
        ("Bard", "ranged", "bard", "dps", "BRD"), ("Machinist", "ranged", "machinist", "dps", "MCH"),
        ("Dancer", "ranged", "dancer", "dps", "DNC"), ("Black Mage", "casters", "black-mage", "dps", "BLM"),
        ("Summoner", "casters", "summoner", "dps", "SMN"), ("Red Mage", "casters", "red-mage", "dps", "RDM"),
        ("Pictomancer", "casters", "pictomancer", "dps", "PCT"), ("Blue Mage", "casters", "blue-mage", "dps", "BLU")];
    public sealed record Recommendation(string Name, string Url, string Fight, string Source,
        string SourceUrl = "", bool Historical = false, bool Community = false, string SheetName = "", string Validated = "");
    private List<Recommendation> recommendations = [];
    private readonly List<Task> pending = [];
    private int job = 17;
    private int source;
    private bool busy;
    private string message = "Choose a job, then find recommended sets.";
    private string sourcePage = "";
    private string fightFilter = "All fights";
    private string setLink = "";
    private bool showHistorical;
    private bool showCommunity;
    private string search = "";
    private readonly Dictionary<string, (DateTime At, List<Recommendation> Sets)> cache = [];
    private DateTime nextLookup;

    public WindowSizeConstraints? TabSizeConstraints => new() { MinimumSize = new(600, 300) };
    public bool ShouldDraw => true;
    public void SetTabState(TabState state) { }
    public void PreDraw()
    {
        pending.RemoveAll(t => t.IsCompleted);
        if (!busy && (resultJob != job || resultSource != source)) recommendations.Clear();
    }
    public void Draw()
    {
        ImGui.TextWrapped("Find gearsets for your job and fight. The Balance includes a metadata-checked Discord resource index; historical and community submissions are optional. Identical gear and materia are skipped.");
        using (ImRaii.Disabled(busy))
        {
            ImGui.Combo("Job", ref job, Jobs.Select(j => j.Job).ToArray(), Jobs.Length);
            ImGui.Combo("Guide", ref source, new[] { "The Balance (Discord + website)", "Icy Veins" }, 2);
            using (var combo = ImRaii.Combo("Fight", fightFilter))
                if (combo)
                    foreach (var fight in RecommendedGearCatalog.Fights)
                        if (ImGui.Selectable(fight, fightFilter == fight)) fightFilter = fight;
            if (ImGui.Button("Find recommended sets")) pending.Add(Find());
        }
        if (sourcePage.Length > 0)
        {
            ImGui.SameLine();
            if (ImGui.Button("Open source guide")) Dalamud.Utility.Util.OpenLink(sourcePage);
        }
        ImGui.TextWrapped(message);
        ImGui.Spacing();
        ImGui.InputText("Set link from guide", ref setLink, 4096);
        using (ImRaii.Disabled(busy || string.IsNullOrWhiteSpace(setLink)))
            if (ImGui.Button("Import pasted Xivgear / Etro link"))
            {
                if (Uri.TryCreate(setLink.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
                    (uri.Host == "xivgear.app" || uri.Host == "www.xivgear.app" || uri.Host == "etro.gg"))
                    pending.Add(Import(new Recommendation("Pasted guide set", setLink.Trim(), fightFilter == "All fights" ? "Unspecified" : fightFilter, "Guide link")));
                else message = "Paste a full HTTPS Xivgear or Etro set link.";
            }
        ImGui.Checkbox("Show historical Endwalker sets", ref showHistorical);
        ImGui.SameLine();
        ImGui.Checkbox("Include community submissions", ref showCommunity);
        ImGui.InputText("Search sets / GCD", ref search, 200);
        var visible = VisibleRecommendations().ToList();
        ImGui.Text($"{visible.Count} matching sets shown");
        ImGui.Separator();
        using var child = ImRaii.Child("recommended_sets", System.Numerics.Vector2.Zero);
        foreach (var (entry, index) in visible.Select((e, i) => (e, i)))
        {
            using var id = ImRaii.PushId(index);
            using (ImRaii.Disabled(busy))
                if (ImGui.Button("Import")) pending.Add(Import(entry));
            ImGui.SameLine();
            if (ImGui.SmallButton("Link")) Dalamud.Utility.Util.OpenLink(entry.Url);
            ImGui.SameLine();
            if (entry.SourceUrl.Length > 0)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Source")) Dalamud.Utility.Util.OpenLink(entry.SourceUrl);
            }
            ImGui.SameLine();
            var flags = (entry.Historical ? "[Historical] " : "") + (entry.Community ? "[Community] " : "");
            var title = string.IsNullOrWhiteSpace(entry.SheetName) || entry.SheetName == entry.Name
                ? entry.Name : $"{entry.SheetName} - {entry.Name}";
            ImGui.TextWrapped($"{flags}[{entry.Fight}] {title}");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{entry.SheetName}\nSource: {entry.Source}\nMetadata checked: {entry.Validated}\n{entry.Url}");
        }
    }
    private int resultJob = -1;
    private int resultSource = -1;
    private IEnumerable<Recommendation> VisibleRecommendations() => recommendations.Where(e =>
        RecommendedGearCatalog.MatchesFight(e.Fight, fightFilter) &&
        (showHistorical || !e.Historical) && (showCommunity || !e.Community) &&
        (string.IsNullOrWhiteSpace(search) || (e.Name + " " + e.SheetName + " " + e.Fight).Contains(search, StringComparison.OrdinalIgnoreCase)));

    private async Task Find()
    {
        busy = true;
        recommendations = [];
        var selected = Jobs[job];
        var requestedFight = fightFilter;
        var provider = source == 0 ? "The Balance" : "Icy Veins";
        sourcePage = source == 0
            ? $"https://www.thebalanceffxiv.com/jobs/{selected.Role}/{selected.Slug}/best-in-slot/"
            : $"https://www.icy-veins.com/ffxiv/{selected.Slug}-pve-{selected.IcyRole}-gear-best-in-slot";
        resultJob = job;
        resultSource = source;
        if (source == 0)
        {
            recommendations = RecommendedGearCatalog.Entries.Where(e => e.Job == selected.Abbreviation)
                .Select(e => new Recommendation(e.Name, e.Url, string.Join(" / ", e.Fights), e.Source,
                    e.SourceUrl, e.Historical, e.Community, e.SheetName, e.Validated))
                .DistinctBy(e => (e.Url, e.Fight, e.Historical, e.Community)).ToList();
            if (VisibleRecommendations().Any())
            {
                message = "Loaded the metadata-checked Balance Discord gear index. Fight filters select matching builds; they never relabel current-raid sets. Link opens the gearset; Source opens its resource post.";
                busy = false;
                return;
            }
        }
        try
        {
            if (cache.TryGetValue(sourcePage, out var cached) && DateTime.UtcNow - cached.At < TimeSpan.FromMinutes(10))
            {
                recommendations.AddRange(cached.Sets);
                message = $"{VisibleRecommendations().Count()} matching sets (guide results cached for ten minutes).";
                return;
            }
            if (DateTime.UtcNow < nextLookup)
            {
                message = "No matching indexed sets. Please wait a minute between guide-page requests, or open the guide and paste its set link below.";
                return;
            }
            nextLookup = DateTime.UtcNow.AddMinutes(1);
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var request = new HttpRequestMessage(HttpMethod.Get, sourcePage);
            request.Headers.UserAgent.ParseAdd("BiSplanner/0.3 (+https://github.com/Jacob5800/DalamudPlugins)");
            using var response = await http.SendAsync(request, timeout.Token);
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                message = "The guide refused automated access (HTTP 403). Open the guide and paste its Xivgear/Etro set link below, or select The Balance to use the Discord index.";
                return;
            }
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(timeout.Token);
            var found = Parse(html, provider);
            cache[sourcePage] = (DateTime.UtcNow, found);
            recommendations.AddRange(found);
            recommendations = recommendations.DistinctBy(e => (e.Url, e.Fight)).ToList();
            message = VisibleRecommendations().Any()
                ? $"Found {VisibleRecommendations().Count()} matching sets."
                : "No matching recommendations found for this job and fight. Current-raid sets are never relabelled as an older fight. Historical and community entries are hidden unless enabled.";
        }
        catch (Exception ex) { message = $"Lookup failed: {ex.Message}. You can paste a set link below."; }
        finally { busy = false; }
    }
    private async Task Import(Recommendation entry)
    {
        busy = true;
        try
        {
            var result = await gearsets.AddGearsetsFromSource(entry.Url.Contains("etro.gg", StringComparison.OrdinalIgnoreCase)
                ? ImportGearsetSourceType.Etro : ImportGearsetSourceType.Xivgear, entry.Url, Jobs[job].Abbreviation);
            if (result.StatusType != GearsetImportStatusType.Success)
            { message = $"Import failed: {result.StatusType}."; return; }
            var assignedFight = fightFilter != "All fights" && RecommendedGearCatalog.MatchesFight(entry.Fight, fightFilter)
                ? fightFilter : entry.Fight;
            foreach (var set in (result.Gearsets ?? []).Concat(result.ExistingDuplicates ?? []).Distinct())
            {
                var fights = set.Fight == "Unspecified" ? [] : set.Fight.Split('/', StringSplitOptions.TrimEntries);
                set.Fight = string.Join(" / ", fights.Concat(assignedFight.Split('/', StringSplitOptions.TrimEntries)).Where(f => f != "Unspecified").Distinct());
            }
            message = $"Imported {result.Gearsets?.Count ?? 0} set(s); skipped {result.SkippedDuplicates} identical build(s).";
        }
        catch (Exception ex) { message = $"Import failed: {ex.Message}"; }
        finally { busy = false; }
    }
    public static List<Recommendation> Parse(string html, string source)
    {
        // Read only headings and published set links; never execute page scripts.
        var tokens = Regex.Matches(html, @"<h[1-6]\b[^>]*>(?<heading>.*?)</h[1-6]>|<(?:a|iframe)\b[^>]*(?:href|src)\s*=\s*[""'](?<url>[^""']+)[""'][^>]*>(?<label>.*?)</(?:a|iframe)>", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
        string Clean(string value) => WebUtility.HtmlDecode(Regex.Replace(value, "<[^>]+>", " ")).Trim();
        var title = "Recommended set";
        var fight = "Current raid";
        var output = new List<Recommendation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match token in tokens)
        {
            if (token.Groups["heading"].Success)
            {
                title = Clean(token.Groups["heading"].Value);
                var lower = title.ToLowerInvariant();
                if (lower.Contains("omega protocol") || Regex.IsMatch(title, @"\bTOP\b")) fight = "TOP";
                else if (lower.Contains("dragonsong")) fight = "DSR";
                else if (lower.Contains("epic of alexander")) fight = "TEA";
                else if (lower.Contains("weapon's refrain") || lower.Contains("weapon’s refrain")) fight = "UWU";
                else if (lower.Contains("unending coil")) fight = "UCOB";
                else if (lower.Contains("futures rewritten")) fight = "FRU";
                else if (lower.Contains("current") || lower.Contains("prog") ) fight = "Current raid";
                continue;
            }
            var url = WebUtility.HtmlDecode(token.Groups["url"].Value);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") continue;
            if (!(uri.Host == "xivgear.app" || uri.Host == "www.xivgear.app" || uri.Host == "etro.gg")) continue;
            if (!(url.Contains("page=", StringComparison.OrdinalIgnoreCase) || uri.AbsolutePath.StartsWith("/gearset/") ||
                uri.AbsolutePath.StartsWith("/sl/") || uri.AbsolutePath.StartsWith("/bis/") || uri.Fragment.StartsWith("#/bis/"))) continue;
            if (!seen.Add(url)) continue;
            var label = Clean(token.Groups["label"].Value);
            if (label.Length < 5 || label.Equals("Xivgear", StringComparison.OrdinalIgnoreCase) || label.Equals("Etro", StringComparison.OrdinalIgnoreCase)) label = title;
            output.Add(new(label, url, fight, source));
        }
        return output;
    }
}
