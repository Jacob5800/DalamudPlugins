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
using System.Threading.Tasks;

namespace BiSplanner.Ui.Renderers.Tabs.Main;

public sealed class RecommendedImportTab(HttpClient http, IGearsetsService gearsets) : TabRenderer<MainWindowTab>
{
    private static readonly (string Job, string Role, string Slug, string IcyRole)[] Jobs = [
        ("Paladin", "tanks", "paladin", "tank"), ("Warrior", "tanks", "warrior", "tank"),
        ("Dark Knight", "tanks", "dark-knight", "tank"), ("Gunbreaker", "tanks", "gunbreaker", "tank"),
        ("White Mage", "healers", "white-mage", "healer"), ("Scholar", "healers", "scholar", "healer"),
        ("Astrologian", "healers", "astrologian", "healer"), ("Sage", "healers", "sage", "healer"),
        ("Monk", "melee", "monk", "dps"), ("Dragoon", "melee", "dragoon", "dps"),
        ("Ninja", "melee", "ninja", "dps"), ("Samurai", "melee", "samurai", "dps"),
        ("Reaper", "melee", "reaper", "dps"), ("Viper", "melee", "viper", "dps"),
        ("Bard", "ranged", "bard", "dps"), ("Machinist", "ranged", "machinist", "dps"),
        ("Dancer", "ranged", "dancer", "dps"), ("Black Mage", "casters", "black-mage", "dps"),
        ("Summoner", "casters", "summoner", "dps"), ("Red Mage", "casters", "red-mage", "dps"),
        ("Pictomancer", "casters", "pictomancer", "dps")];
    public sealed record Recommendation(string Name, string Url, string Fight, string Source);
    private List<Recommendation> recommendations = [];
    private readonly List<Task> pending = [];
    private int job = 17;
    private int source;
    private bool busy;
    private string message = "Choose a job, then find recommended sets.";
    private string sourcePage = "";
    private string fightOverride = "Automatic";
    private string setLink = "";
    private readonly Dictionary<string, (DateTime At, List<Recommendation> Sets)> cache = [];
    private DateTime nextLookup;

    public WindowSizeConstraints? TabSizeConstraints => new() { MinimumSize = new(600, 300) };
    public bool ShouldDraw => true;
    public void SetTabState(TabState state) { }
    public void PreDraw() { pending.RemoveAll(t => t.IsCompleted); }
    public void Draw()
    {
        ImGui.TextWrapped("Import sets linked by The Balance or Icy Veins. These are guide recommendations, not a popularity ranking. Identical gear and materia are skipped.");
        using (ImRaii.Disabled(busy))
        {
            ImGui.Combo("Job", ref job, Jobs.Select(j => j.Job).ToArray(), Jobs.Length);
            ImGui.Combo("Guide", ref source, new[] { "The Balance", "Icy Veins" }, 2);
            using (var combo = ImRaii.Combo("Fight for imports", fightOverride))
                if (combo)
                    foreach (var fight in new[] { "Automatic", "Current raid", "FRU", "TOP", "DSR", "TEA", "UWU", "UCOB" })
                        if (ImGui.Selectable(fight, fightOverride == fight)) fightOverride = fight;
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
                    pending.Add(Import(new Recommendation("Pasted guide set", setLink.Trim(), fightOverride == "Automatic" ? "Unspecified" : fightOverride, "Guide link")));
                else message = "Paste a full HTTPS Xivgear or Etro set link.";
            }
        ImGui.Separator();
        using var child = ImRaii.Child("recommended_sets", System.Numerics.Vector2.Zero);
        foreach (var (entry, index) in recommendations.Select((e, i) => (e, i)))
        {
            using var id = ImRaii.PushId(index);
            using (ImRaii.Disabled(busy))
                if (ImGui.Button("Import")) pending.Add(Import(entry));
            ImGui.SameLine();
            if (ImGui.SmallButton("Link")) Dalamud.Utility.Util.OpenLink(entry.Url);
            ImGui.SameLine();
            ImGui.TextWrapped($"[{entry.Fight}] {entry.Name}");
        }
    }
    private async Task Find()
    {
        busy = true;
        recommendations = [];
        var selected = Jobs[job];
        var provider = source == 0 ? "The Balance" : "Icy Veins";
        sourcePage = source == 0
            ? $"https://www.thebalanceffxiv.com/jobs/{selected.Role}/{selected.Slug}/best-in-slot/"
            : $"https://www.icy-veins.com/ffxiv/{selected.Slug}-pve-{selected.IcyRole}-gear-best-in-slot";
        try
        {
            if (cache.TryGetValue(sourcePage, out var cached) && DateTime.UtcNow - cached.At < TimeSpan.FromMinutes(10))
            {
                recommendations = cached.Sets;
                message = $"Found {recommendations.Count} linked sets (cached for 10 minutes).";
                return;
            }
            if (DateTime.UtcNow < nextLookup)
            {
                message = "Please wait a minute between guide requests, or paste a set link below.";
                return;
            }
            nextLookup = DateTime.UtcNow.AddMinutes(1);
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var request = new HttpRequestMessage(HttpMethod.Get, sourcePage);
            request.Headers.UserAgent.ParseAdd("BiSplanner/0.3 (+https://github.com/Jacob5800/DalamudPlugins)");
            using var response = await http.SendAsync(request, timeout.Token);
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                message = "The guide refused automated access (HTTP 403). Open the source guide, copy its Xivgear/Etro set link, and paste it below. No retry is sent automatically.";
                return;
            }
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(timeout.Token);
            recommendations = Parse(html, provider);
            cache[sourcePage] = (DateTime.UtcNow, recommendations);
            message = recommendations.Count == 0
                ? "No supported Xivgear/Etro set links found. Open the source guide to check for spreadsheets or changed page structure."
                : $"Found {recommendations.Count} linked sets. Review their fight labels before importing.";
        }
        catch (Exception ex) { message = $"Guide lookup failed: {ex.Message}. You can still import a set URL using + on Gearsets."; }
        finally { busy = false; }
    }
    private async Task Import(Recommendation entry)
    {
        busy = true;
        try
        {
            var result = await gearsets.AddGearsetsFromSource(entry.Url.Contains("etro.gg", StringComparison.OrdinalIgnoreCase)
                ? ImportGearsetSourceType.Etro : ImportGearsetSourceType.Xivgear, entry.Url);
            if (result.StatusType != GearsetImportStatusType.Success)
            { message = $"Import failed: {result.StatusType}."; return; }
            foreach (var set in result.Gearsets ?? [])
            {
                set.Fight = fightOverride == "Automatic" ? entry.Fight : fightOverride;
                if (!set.Name.Contains(set.Fight, StringComparison.OrdinalIgnoreCase))
                {
                    var baseName = $"{set.Name} ({set.Fight})";
                    var candidate = baseName;
                    var suffix = 1;
                    while (gearsets.CurrentGearsets.Any(g => g != set && g.Name == candidate)) candidate = $"{baseName} ({suffix++})";
                    set.Name = candidate;
                }
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
            if (!(url.Contains("page=", StringComparison.OrdinalIgnoreCase) || uri.AbsolutePath.StartsWith("/gearset/"))) continue;
            if (!seen.Add(url)) continue;
            var label = Clean(token.Groups["label"].Value);
            if (label.Length < 5 || label.Equals("Xivgear", StringComparison.OrdinalIgnoreCase) || label.Equals("Etro", StringComparison.OrdinalIgnoreCase)) label = title;
            output.Add(new(label, url, fight, source));
        }
        return output;
    }
}
