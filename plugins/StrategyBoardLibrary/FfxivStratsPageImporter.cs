using System.Net.Http;
using System.Net;
using System.Text;

namespace StrategyBoardLibrary;

internal sealed record PageImportResult(string? ShareCode, string? Title, string Message);

internal static class FfxivStratsPageImporter
{
    private static readonly HttpClient Client = CreateClient();
    private const int MaximumPageBytes = 3_000_000;

    public static async Task<PageImportResult> FetchAsync(string rawUrl)
    {
        if (!TryGetAllowedUri(rawUrl, out var uri))
        {
            return new PageImportResult(null, null, "Paste a valid https://ffxivstrats.io/strategy/... URL.");
        }

        try
        {
            for (var redirectCount = 0; redirectCount <= 3; redirectCount++)
            {
                using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (IsRedirect(response.StatusCode))
                {
                    if (redirectCount == 3 || response.Headers.Location is null
                        || !Uri.TryCreate(uri, response.Headers.Location, out var redirectedUri)
                        || !TryGetAllowedUri(redirectedUri.ToString(), out uri))
                        return new PageImportResult(null, null, "The site redirected outside its allowed address. Open the link in a browser and paste the share code here.");
                    continue;
                }

                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is > MaximumPageBytes)
                    return new PageImportResult(null, null, "That page is too large to inspect safely.");

                await using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var output = new MemoryStream();
                var buffer = new byte[16 * 1024];
                while (true)
                {
                    var read = await input.ReadAsync(buffer).ConfigureAwait(false);
                    if (read == 0)
                        break;
                    if (output.Length + read > MaximumPageBytes)
                        return new PageImportResult(null, null, "That page is too large to inspect safely.");
                    output.Write(buffer, 0, read);
                }

                var html = Encoding.UTF8.GetString(output.ToArray());
                if (html.Length == 0)
                    return new PageImportResult(null, null, "The page was empty.");

                var title = ShareCodeTools.ExtractPageTitle(html);
                var code = ShareCodeTools.TryExtract(html, out var extracted) ? extracted : null;
                var message = code is null
                    ? "The page loaded, but its HTML doesn't contain a share code. Copy the code from the site and paste it below."
                    : "Share code found. Review the title and save it to your library.";
                return new PageImportResult(code, title, message);
            }

            return new PageImportResult(null, null, "The site redirected too many times.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new PageImportResult(null, null, $"Couldn't read that page: {ex.Message}");
        }
    }

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("StrategyBoardLibrary/0.1 (Dalamud)");
        return client;
    }

    private static bool TryGetAllowedUri(string rawUrl, out Uri uri)
    {
        if (Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps
            && (parsed.Host.Equals("ffxivstrats.io", StringComparison.OrdinalIgnoreCase)
                || parsed.Host.Equals("www.ffxivstrats.io", StringComparison.OrdinalIgnoreCase)))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther
        or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
}
