namespace StrategyBoardLibrary;

internal sealed class CatalogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Encounter { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string SourceUrl { get; set; } = string.Empty;
    public string ShareCode { get; set; } = string.Empty;
    public bool Favorite { get; set; }
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
}
