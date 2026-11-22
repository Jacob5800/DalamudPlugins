using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace RetainerPricer;

internal sealed class FeedbackClient : IDisposable
{
    // The relay URL is public (it ships in this open-source plugin). Only the recipient address stays in the
    // relay's private Script Properties, so the relay itself limits volume and rejects duplicates.
    private static readonly TimeSpan MinimumSendInterval = TimeSpan.FromSeconds(60);
    private long lastAttemptTimestamp;
    private const string RelayEndpoint = "https://script.google.com/macros/s/AKfycbxj_5FgQqQO3KQoca5lB61-c7TktQXY5FTtwrlwhL2UBZA8zJ0kq1gc6i-txDW63H912w/exec";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public bool IsConfigured => Uri.TryCreate(RelayEndpoint, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps;

    public async Task SendAsync(string message, string pluginVersion, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Feedback delivery is not configured yet.");
        if (string.IsNullOrWhiteSpace(message) || message.Trim().Length > 4000)
            throw new ArgumentException("Feedback must contain 1 to 4000 characters.", nameof(message));
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var previous = Interlocked.Exchange(ref lastAttemptTimestamp, now);
        if (previous != 0 && System.Diagnostics.Stopwatch.GetElapsedTime(previous, now) < MinimumSendInterval)
        {
            // Keep the earlier timestamp so repeated clicks cannot extend the wait.
            Interlocked.Exchange(ref lastAttemptTimestamp, previous);
            var remaining = MinimumSendInterval - System.Diagnostics.Stopwatch.GetElapsedTime(previous, now);
            throw new InvalidOperationException($"Please wait {Math.Ceiling(remaining.TotalSeconds):N0} more seconds before sending another note.");
        }

        var payload = JsonSerializer.Serialize(new FeedbackRequest(message.Trim(), pluginVersion), JsonOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(RelayEndpoint, content, cancellationToken).ConfigureAwait(false);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Feedback relay returned an unsuccessful response.");

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
        {
            var reason = root.TryGetProperty("error", out var error) ? error.GetString() : null;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(reason)
                ? "Feedback could not be sent. Please try again later."
                : reason);
        }
    }

    public void Dispose() => http.Dispose();

    private sealed record FeedbackRequest(string Message, string PluginVersion);
}
