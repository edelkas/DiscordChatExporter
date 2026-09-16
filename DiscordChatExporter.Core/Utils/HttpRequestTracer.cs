using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordChatExporter.Core.Utils;

// Counts outgoing requests per API route, so that the cost of an export can be measured rather
// than guessed at. Entirely opt-in: without the environment variable, the handler is never
// installed and nothing is recorded.
public partial class HttpRequestTracer : DelegatingHandler
{
    private readonly ConcurrentDictionary<string, int> _countsByRoute = new(StringComparer.Ordinal);

    public HttpRequestTracer(HttpMessageHandler innerHandler)
        : base(innerHandler) { }

    public IReadOnlyList<KeyValuePair<string, int>> Counts =>
        _countsByRoute
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Key, StringComparer.Ordinal)
            .ToArray();

    public int TotalCount => _countsByRoute.Values.Sum();

    // Counted separately, because the retry pipeline re-sends a throttled request and each
    // attempt shows up here. Without this, a rate-limited run looks like it made more calls.
    private int _rateLimitedCount;

    public int RateLimitedCount => Volatile.Read(ref _rateLimitedCount);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        if (request.RequestUri is not null)
        {
            _countsByRoute.AddOrUpdate(
                NormalizeRoute(request.RequestUri),
                1,
                (_, count) => count + 1
            );
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            Interlocked.Increment(ref _rateLimitedCount);

        return response;
    }
}

public partial class HttpRequestTracer
{
    public const string EnvironmentVariableName = "DISCORDCHATEXPORTER_HTTP_TRACE";

    public static bool IsEnabled { get; } =
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariableName));

    // Collapses IDs into placeholders, so that (for example) every member lookup is counted
    // against a single 'guilds/{id}/members/{id}' row instead of one row per user.
    [GeneratedRegex(@"\d{17,20}")]
    private static partial Regex SnowflakeRegex { get; }

    // Reaction routes end in the emoji itself, which is neither an ID nor a fixed segment
    [GeneratedRegex(@"(?<=/reactions/).+$")]
    private static partial Regex ReactionEmojiRegex { get; }

    private static string NormalizeRoute(Uri uri)
    {
        // Query strings only carry pagination cursors and limits, which aren't part of the route
        var path = uri.AbsolutePath.TrimStart('/');

        // Strip the API version prefix to keep the output narrow
        if (path.StartsWith("api/", StringComparison.Ordinal))
            path = path["api/".Length..];
        if (path.StartsWith("v10/", StringComparison.Ordinal))
            path = path["v10/".Length..];

        return SnowflakeRegex.Replace(ReactionEmojiRegex.Replace(path, "{emoji}"), "{id}");
    }
}
