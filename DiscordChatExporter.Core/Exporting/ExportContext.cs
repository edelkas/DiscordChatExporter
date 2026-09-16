using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Utils;
using PowerKit.Extensions;

namespace DiscordChatExporter.Core.Exporting;

internal class ExportContext(DiscordClient discord, ExportRequest request, GuildCache guildCache)
{
    // The asset downloader deliberately stays per-channel: its internal bookkeeping is not
    // thread-safe, and a context is only ever written to by one channel at a time.
    private readonly ExportAssetDownloader _assetDownloader = new(
        request.AssetsDirPath,
        request.ShouldReuseAssets
    );

    public DiscordClient Discord { get; } = discord;

    public ExportRequest Request { get; } = request;

    public DateTimeOffset NormalizeDate(DateTimeOffset instant) =>
        Request.IsUtcNormalizationEnabled ? instant.ToUniversalTime() : instant.ToLocalTime();

    public string FormatDate(DateTimeOffset instant, string format = "g") =>
        NormalizeDate(instant).ToString(format, Request.CultureInfo);

    // Kept for the call site in ChannelExporter; the underlying fetch now runs once per guild
    // per run rather than once per channel.
    public async ValueTask PopulateChannelsAndRolesAsync(
        CancellationToken cancellationToken = default
    ) => await guildCache.EnsureInitializedAsync(cancellationToken);

    public async ValueTask PopulateChannelAsync(
        Snowflake id,
        CancellationToken cancellationToken = default
    ) => await guildCache.PopulateChannelAsync(id, cancellationToken);

    public async ValueTask PopulateMemberAsync(
        Snowflake id,
        CancellationToken cancellationToken = default
    ) => await guildCache.PopulateMemberAsync(id, null, cancellationToken);

    public async ValueTask PopulateMemberAsync(
        User user,
        CancellationToken cancellationToken = default
    ) => await guildCache.PopulateMemberAsync(user.Id, user, cancellationToken);

    public Member? TryGetMember(Snowflake id) => guildCache.TryGetMember(id);

    public Channel? TryGetChannel(Snowflake id) => guildCache.TryGetChannel(id);

    public Role? TryGetRole(Snowflake id) => guildCache.TryGetRole(id);

    public IReadOnlyList<Role> GetUserRoles(Snowflake id) =>
        TryGetMember(id)
            ?.RoleIds.Select(TryGetRole)
            .WhereNotNull()
            .OrderByDescending(r => r.Position)
            .ToArray()
        ?? [];

    public Color? TryGetUserColor(Snowflake id) =>
        GetUserRoles(id).Where(r => r.Color is not null).Select(r => r.Color).FirstOrDefault();

    public async ValueTask<string> ResolveAssetUrlAsync(
        string url,
        CancellationToken cancellationToken = default
    )
    {
        if (!Request.ShouldDownloadAssets)
            return url;

        try
        {
            var filePath = await _assetDownloader.DownloadAsync(url, cancellationToken);
            var relativeFilePath = Path.GetRelativePath(Request.OutputDirPath, filePath);

            // Prefer the relative path so that the export package can be copied around without breaking references.
            // However, if the assets directory lies outside the export directory, use the absolute path instead.
            var shouldUseAbsoluteFilePath =
                relativeFilePath.StartsWith(
                    ".." + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal
                )
                || relativeFilePath.StartsWith(
                    ".." + Path.AltDirectorySeparatorChar,
                    StringComparison.Ordinal
                );

            var optimalFilePath = shouldUseAbsoluteFilePath ? filePath : relativeFilePath;

            // For HTML, the path needs to be properly formatted
            if (Request.Format is ExportFormat.HtmlDark or ExportFormat.HtmlLight)
                return Url.EncodeFilePath(optimalFilePath);

            return optimalFilePath;
        }
        // Try to catch only exceptions related to failed HTTP requests
        // https://github.com/Tyrrrz/DiscordChatExporter/issues/332
        // https://github.com/Tyrrrz/DiscordChatExporter/issues/372
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // We don't want this to crash the exporting process in case of failure.
            // TODO: add logging so we can be more liberal with catching exceptions.
            return url;
        }
    }
}
