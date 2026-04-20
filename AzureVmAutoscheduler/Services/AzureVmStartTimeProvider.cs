using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using AzureVmAutoscheduler.Services.Interfaces;

namespace AzureVmAutoscheduler.Services;

public sealed class AzureVmStartTimeProvider : IVmStartTimeProvider
{
    private const string ManagementScope = "https://management.azure.com/.default";
    private const string StartOperationName = "Microsoft.Compute/virtualMachines/start/action";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromDays(30);

    private readonly TokenCredential _tokenCredential;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureVmStartTimeProvider> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public AzureVmStartTimeProvider(
        TokenCredential tokenCredential,
        HttpClient httpClient,
        ILogger<AzureVmStartTimeProvider> logger)
    {
        _tokenCredential = tokenCredential;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DateTime?> GetLastStartTimeUtcAsync(string subscriptionId, string resourceId, CancellationToken cancellationToken)
    {
        var cacheKey = $"{subscriptionId}|{resourceId}";
        if (_cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.FetchedAtUtc < CacheLifetime)
        {
            return cached.StartedAtUtc;
        }

        try
        {
            var accessToken = await _tokenCredential.GetTokenAsync(
                new TokenRequestContext([ManagementScope]),
                cancellationToken);

            var windowEnd = DateTime.UtcNow;
            var windowStart = windowEnd - LookbackWindow;
            var filter = string.Join(" and ",
                $"eventTimestamp ge '{windowStart:O}'",
                $"eventTimestamp le '{windowEnd:O}'",
                $"resourceUri eq '{resourceId}'",
                $"operationName/value eq '{StartOperationName}'",
                "status/value eq 'Succeeded'");

            var requestUri =
                $"https://management.azure.com/subscriptions/{subscriptionId}/providers/microsoft.insights/eventtypes/management/values" +
                $"?api-version=2015-04-01&$filter={Uri.EscapeDataString(filter)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            DateTime? startedAtUtc = null;
            if (document.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in valueElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("eventTimestamp", out var timestampElement))
                    {
                        continue;
                    }

                    if (!DateTime.TryParse(timestampElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var timestamp))
                    {
                        continue;
                    }

                    if (!startedAtUtc.HasValue || timestamp > startedAtUtc.Value)
                    {
                        startedAtUtc = timestamp;
                    }
                }
            }

            _cache[cacheKey] = new CacheEntry(startedAtUtc, DateTime.UtcNow);
            return startedAtUtc;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve last start time for resource {ResourceId}", resourceId);
            _cache[cacheKey] = new CacheEntry(null, DateTime.UtcNow);
            return null;
        }
    }

    private sealed record CacheEntry(DateTime? StartedAtUtc, DateTime FetchedAtUtc);
}
