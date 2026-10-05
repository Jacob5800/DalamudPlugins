using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace StrategyBoardLibrary;

internal sealed class MainWindow : Window
{
    private readonly CatalogStore catalog;
    private string search = string.Empty;
    private string title = string.Empty;
    private string encounter = string.Empty;
    private string tags = string.Empty;
    private string sourceUrl = string.Empty;
    private string shareCode = string.Empty;
    private string libraryJson = string.Empty;
    private CatalogEntry? selected;
    private Task<PageImportResult>? pageFetchTask;
    private string status = "Paste a share code, or try importing a FFXIVStrats page URL.";
    private bool favoritesOnly;
    private bool importPanelOpen = true;

    public MainWindow(CatalogStore catalog) : base("Strategy Board Library")
    {
        this.catalog = catalog;
        Size = new Vector2(780, 680);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags = ImGuiWindowFlags.NoCollapse;
    }

    public override void Draw()
    {
        DrawStatus();
        ImGui.Separator();
        DrawSearch();
        ImGui.Separator();
        DrawCatalog();
        ImGui.Separator();
        DrawSelectedBoard();
        ImGui.Separator();
        DrawImportPanel();
        ImGui.Separator();
        DrawLibraryTransfer();
    }

    private void DrawStatus()
    {
        ImGui.TextWrapped("Keep a searchable library outside the game's 50 saved-board slots. Pick a board, copy its code, then paste it in the game's Strategy Board Import screen.");
        if (!string.IsNullOrWhiteSpace(catalog.LastError))
            ImGui.TextColored(new Vector4(1f, 0.55f, 0.35f, 1f), catalog.LastError);
        else if (!string.IsNullOrWhiteSpace(status))
            ImGui.TextWrapped(status);
    }

    private void DrawSearch()
    {
        ImGui.SetNextItemWidth(-130f);
        ImGui.InputTextWithHint("##stratSearch", "Search title, encounter, or tags...", ref search, 256);
        ImGui.SameLine();
        if (ImGui.Button(favoritesOnly ? "★ Favorites" : "☆ Favorites"))
            favoritesOnly = !favoritesOnly;
        ImGui.TextDisabled($"{GetVisibleEntries().Count} matching / {catalog.Entries.Count} saved");
    }

    private void DrawCatalog()
    {
        var entries = GetVisibleEntries();
        if (entries.Count == 0)
        {
            ImGui.TextDisabled(catalog.Entries.Count == 0
                ? "Your library is empty. Add one share code below or import a library JSON file."
                : "No boards match that search.");
            return;
        }

        var availableHeight = Math.Clamp(ImGui.GetContentRegionAvail().Y * 0.34f, 110f, 260f);
        if (ImGui.BeginChild("##boardCatalog", new Vector2(0, availableHeight), true))
        {
            foreach (var entry in entries)
            {
                ImGui.PushID(entry.Id.ToString("N"));
                var isSelected = selected?.Id == entry.Id;
                if (ImGui.Selectable($"{(entry.Favorite ? "★ " : string.Empty)}{entry.Title}##entry", isSelected))
                    selected = entry;
                ImGui.SameLine(320f);
                ImGui.TextDisabled(entry.Encounter);
                ImGui.SameLine(490f);
                ImGui.TextDisabled(string.Join(", ", entry.Tags));
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
    }

    private void DrawSelectedBoard()
    {
        if (selected is null)
        {
            ImGui.TextDisabled("Select a board to see its source and import actions.");
            return;
        }

        ImGui.Text($"Selected: {selected.Title}");
        if (!string.IsNullOrWhiteSpace(selected.Encounter))
            ImGui.TextDisabled($"Encounter: {selected.Encounter}");
        if (selected.Tags.Count > 0)
            ImGui.TextDisabled($"Tags: {string.Join(", ", selected.Tags)}");
        if (!string.IsNullOrWhiteSpace(selected.SourceUrl))
        {
            ImGui.TextWrapped(selected.SourceUrl);
            ImGui.SameLine();
            if (ImGui.SmallButton("Copy source URL"))
            {
                ImGui.SetClipboardText(selected.SourceUrl);
                status = "Source link copied.";
            }
        }

        if (ImGui.Button("Copy share code for in-game import"))
        {
            ImGui.SetClipboardText(selected.ShareCode);
            status = "Share code copied. Open Strategy Board > Import in-game and paste it.";
        }
        ImGui.SameLine();
        if (ImGui.Button(selected.Favorite ? "Remove favorite" : "Add favorite"))
        {
            selected.Favorite = !selected.Favorite;
            catalog.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Remove from library"))
        {
            catalog.Remove(selected);
            selected = null;
            status = "Board removed from your local library.";
        }
    }

    private void DrawImportPanel()
    {
        importPanelOpen = ImGui.CollapsingHeader("Add a board", importPanelOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
        if (!importPanelOpen)
            return;

        ImGui.TextDisabled("Fastest: paste a [stgy:...] share code. You can also try a FFXIVStrats strategy link.");
        if (ImGui.Button("Read code or link from clipboard"))
            ReadClipboardImport();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##sourceUrl", "https://ffxivstrats.io/strategy/...", ref sourceUrl, 512);
        ImGui.BeginDisabled(pageFetchTask is { IsCompleted: false });
        if (ImGui.Button("Try import from URL"))
            StartPageImport();
        ImGui.EndDisabled();
        ConsumePageFetch();

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##boardTitle", "Board title", ref title, 128);
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##boardEncounter", "Encounter / duty", ref encounter, 128);
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##boardTags", "Tags separated by commas (e.g. LPDU, PF, uptime)", ref tags, 256);
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextMultiline("##shareCode", ref shareCode, 12000, new Vector2(-1f, 76f));

        if (ImGui.Button("Add to library"))
            AddBoard();
        ImGui.SameLine();
        if (ImGui.Button("Clear fields"))
        {
            title = string.Empty;
            encounter = string.Empty;
            tags = string.Empty;
            sourceUrl = string.Empty;
            shareCode = string.Empty;
            status = "Ready for a new board.";
        }
    }

    private void DrawLibraryTransfer()
    {
        if (!ImGui.CollapsingHeader("Back up or move your library"))
            return;

        ImGui.TextDisabled("Export JSON to move your collection to another PC. Paste a prior export below to merge it; duplicates are skipped.");
        if (ImGui.Button("Copy library JSON"))
        {
            ImGui.SetClipboardText(catalog.ExportJson());
            status = "Library JSON copied to the clipboard.";
        }
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextMultiline("##libraryJson", ref libraryJson, 500000, new Vector2(-1f, 80f));
        if (ImGui.Button("Merge pasted library JSON"))
        {
            if (catalog.ImportJson(libraryJson, out _, out var message))
                status = message;
            else
                status = message;
        }
    }

    private List<CatalogEntry> GetVisibleEntries()
    {
        var query = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return catalog.Entries
            .Where(entry => !favoritesOnly || entry.Favorite)
            .Where(entry => query.All(term =>
                entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || entry.Encounter.Contains(term, StringComparison.OrdinalIgnoreCase)
                || entry.SourceUrl.Contains(term, StringComparison.OrdinalIgnoreCase)
                || entry.Tags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(entry => entry.Favorite)
            .ThenBy(entry => entry.Encounter, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void AddBoard()
    {
        if (!ShareCodeTools.TryExtract(shareCode, out var code))
        {
            status = "Paste a valid [stgy:...] Strategy Board share code first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            status = "Give the board a title so it is easy to find later.";
            return;
        }

        var entry = new CatalogEntry
        {
            Title = title.Trim(),
            Encounter = encounter.Trim(),
            Tags = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SourceUrl = sourceUrl.Trim(),
            ShareCode = code,
        };
        if (!catalog.Add(entry))
        {
            status = catalog.LastError ?? "Couldn't add that board.";
            return;
        }

        selected = entry;
        shareCode = string.Empty;
        status = "Board saved. It will now appear in search and remain available outside the game's board limit.";
    }

    private void ReadClipboardImport()
    {
        var clipboard = GetClipboardText()?.Trim();
        if (ShareCodeTools.TryExtract(clipboard, out var code))
        {
            shareCode = code;
            status = "Share code copied from clipboard. Add a title and save it to your library.";
            return;
        }

        if (Uri.TryCreate(clipboard, UriKind.Absolute, out var uri)
            && (uri.Host.Equals("ffxivstrats.io", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("www.ffxivstrats.io", StringComparison.OrdinalIgnoreCase)))
        {
            sourceUrl = uri.ToString();
            StartPageImport();
            return;
        }

        status = "Clipboard doesn't contain a Strategy Board code or FFXIVStrats link.";
    }

    private void StartPageImport()
    {
        pageFetchTask = FfxivStratsPageImporter.FetchAsync(sourceUrl);
        status = "Checking the page for its share code...";
    }

    private unsafe string? GetClipboardText()
    {
        byte* pointer = ImGuiNative.GetClipboardText();
        if (pointer == null)
            return null;

        var byteCount = 0;
        while (pointer[byteCount] != 0)
            byteCount++;
        return Encoding.UTF8.GetString(pointer, byteCount);
    }

    private void ConsumePageFetch()
    {
        if (pageFetchTask is not { IsCompleted: true } task)
            return;

        pageFetchTask = null;
        try
        {
            var result = task.GetAwaiter().GetResult();
            status = result.Message;
            if (result.ShareCode is not null)
                shareCode = result.ShareCode;
            if (!string.IsNullOrWhiteSpace(result.Title) && string.IsNullOrWhiteSpace(title))
                title = result.Title;
            if (string.IsNullOrWhiteSpace(title) && Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
                title = $"Strategy {uri.Segments.Last().Trim('/')}";
        }
        catch (Exception ex)
        {
            status = $"Couldn't inspect the page: {ex.Message}";
        }
    }
}
