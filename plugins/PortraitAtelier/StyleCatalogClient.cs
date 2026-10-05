using System.Net.Http;
using System.Text.Json;

namespace PortraitAtelier;

internal static class StyleCatalogClient
{
    private const string CatalogUrl = "https://raw.githubusercontent.com/Jacob5800/PortraitAtelier/main/catalog/styles.json";
    private const int MaximumCatalogBytes = 512 * 1024;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<StyleCatalogResult> DownloadAsync()
    {
        try
        {
            using var response = await Client.GetAsync(CatalogUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return StyleCatalogResult.Failed($"Style catalog request failed ({(int)response.StatusCode}). Built-in looks are still available.");

            if (response.Content.Headers.ContentLength is > MaximumCatalogBytes)
                return StyleCatalogResult.Failed("The style catalog is too large to load safely.");

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaximumCatalogBytes)
                    return StyleCatalogResult.Failed("The style catalog is too large to load safely.");
                buffer.Write(chunk, 0, read);
            }

            var document = JsonSerializer.Deserialize<StyleCatalogDocument>(buffer.ToArray(), JsonOptions);
            if (document is null || document.SchemaVersion != 1 || document.Styles is null || document.Styles.Count > 250)
                return StyleCatalogResult.Failed("The style catalog has an unsupported format.");

            var valid = document.Styles
                .Where(IsValid)
                .GroupBy(style => style.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToArray();

            return StyleCatalogResult.Succeeded(valid);
        }
        catch (OperationCanceledException)
        {
            return StyleCatalogResult.Failed("The style catalog request timed out. Built-in looks are still available.");
        }
        catch (HttpRequestException)
        {
            return StyleCatalogResult.Failed("Could not reach the style catalog. Built-in looks are still available.");
        }
        catch (JsonException)
        {
            return StyleCatalogResult.Failed("The public style catalog contains invalid JSON.");
        }
    }

    private static bool IsValid(PortraitStyle? style) =>
        style is not null
        && !string.IsNullOrWhiteSpace(style.Id)
        && style.Id.Length <= 64
        && style.Id.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
        && !string.IsNullOrWhiteSpace(style.Name)
        && style.Name.Length <= 80
        && !string.IsNullOrWhiteSpace(style.Category)
        && style.Category.Length <= 40
        && !string.IsNullOrWhiteSpace(style.Description)
        && style.Description.Length <= 240
        && style.Seed > 0
        && style.CameraDistance is >= 0.75f and <= 1.3f
        && style.CameraTargetOffset is >= -0.08f and <= 0.08f
        && style.DirectionalBrightness is >= 0.6f and <= 1.4f
        && style.AmbientBrightness is >= 0.6f and <= 1.4f;
}

internal sealed record StyleCatalogDocument(int SchemaVersion, List<PortraitStyle>? Styles);

internal sealed record StyleCatalogResult(bool Success, IReadOnlyList<PortraitStyle> Styles, string Message)
{
    public static StyleCatalogResult Succeeded(IReadOnlyList<PortraitStyle> styles) =>
        new(true, styles, "");

    public static StyleCatalogResult Failed(string message) =>
        new(false, Array.Empty<PortraitStyle>(), message);
}
