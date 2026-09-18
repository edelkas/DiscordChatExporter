using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using DiscordChatExporter.Cli.Commands.Converters;
using DiscordChatExporter.Cli.Commands.Shared;
using DiscordChatExporter.Cli.Utils;
using DiscordChatExporter.Cli.Utils.Extensions;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Exceptions;
using DiscordChatExporter.Core.Exporting;
using DiscordChatExporter.Core.Exporting.Filtering;
using DiscordChatExporter.Core.Exporting.Partitioning;
using DiscordChatExporter.Core.Utils;
using Gress;
using Spectre.Console;

namespace DiscordChatExporter.Cli.Commands.Base;

public abstract class ExportCommandBase : DiscordCommandBase
{
    [CommandOption(
        "output",
        'o',
        Description = "Output file or directory path. "
            + "If a directory is specified, file names will be generated automatically based on the channel names and export parameters. "
            + "Directory paths must end with a slash to avoid ambiguity. "
            + "Supports template tokens, see the documentation for more info."
    )]
    public string OutputPath
    {
        get;
        // Handle ~/ in paths on Unix systems
        // https://github.com/Tyrrrz/DiscordChatExporter/pull/903
        set => field = Path.GetFullPath(value);
    } = Directory.GetCurrentDirectory();

    [CommandOption("format", 'f', Description = "Export format.")]
    public ExportFormat ExportFormat { get; set; } = ExportFormat.HtmlDark;

    [CommandOption(
        "after",
        Description = "Only include messages sent after this date or message ID."
    )]
    public Snowflake? After { get; set; }

    [CommandOption(
        "before",
        Description = "Only include messages sent before this date or message ID."
    )]
    public Snowflake? Before { get; set; }

    [CommandOption(
        "partition",
        'p',
        Description = "Split the output into partitions, each limited to the specified "
            + "number of messages (e.g., '100') or file size (e.g., '10mb')."
    )]
    public PartitionLimit PartitionLimit { get; set; } = PartitionLimit.Null;

    [CommandOption(
        "include-threads",
        Description = "Which types of threads should be included: "
            + "'none', 'active', 'archived', 'all', or 'only' to export the threads "
            + "without the channels they live in.",
        Converter = typeof(ThreadInclusionModeInputConverter)
    )]
    public ThreadInclusionMode ThreadInclusionMode { get; set; } = ThreadInclusionMode.None;

    [CommandOption(
        "filter",
        Description = "Only include messages that satisfy this filter. "
            + "See the documentation for more info."
    )]
    public MessageFilter MessageFilter { get; set; } = MessageFilter.Null;

    [CommandOption(
        "parallel",
        Description = "Limits how many channels can be exported in parallel."
    )]
    public int ParallelLimit { get; set; } = 1;

    [CommandOption(
        "reverse",
        Description = "Export messages in reverse chronological order (newest first)."
    )]
    public bool IsReverseMessageOrder { get; set; }

    [CommandOption(
        "markdown",
        Description = "Process markdown, mentions, and other special tokens."
    )]
    public bool ShouldFormatMarkdown { get; set; } = true;

    [CommandOption(
        "reaction-users",
        Description = "Fetch the list of users behind each reaction. "
            + "Only affects the JSON format, and is by far the most expensive part of an export: "
            + "it costs one request per 100 users per reaction, which routinely dwarfs the cost of "
            + "fetching the messages themselves. Disable it to keep only the emoji and the count."
    )]
    public bool ShouldFetchReactionUsers { get; set; } = true;

    [CommandOption(
        "media",
        Description = "Download assets referenced by the export (user avatars, attached files, embedded images, etc.)."
    )]
    public bool ShouldDownloadAssets { get; set; }

    [CommandOption(
        "reuse-media",
        Description = "Reuse previously downloaded assets to avoid redundant requests."
    )]
    public bool ShouldReuseAssets { get; set; } = false;

    [CommandOption(
        "media-dir",
        Description = "Download assets to this directory. "
            + "If not specified, the asset directory path will be derived from the output path."
    )]
    public string? AssetsDirPath
    {
        get;
        // Handle ~/ in paths on Unix systems
        // https://github.com/Tyrrrz/DiscordChatExporter/pull/903
        set => field = value is not null ? Path.GetFullPath(value) : null;
    }

    [CommandOption(
        "extended",
        Description = "Include fields that this fork exports but the original DiscordChatExporter "
            + "does not. Without it, the exported schema stays compatible with vanilla DCE. "
            + "Currently affects the JSON format only."
    )]
    public bool IsExtended { get; set; } = false;

    [CommandOption(
        "normal",
        Description = "Normalize the JSON output: entities that have an identity (users, roles, "
            + "emojis, stickers) are emitted once into lookup tables at the root of the document, "
            + "and referenced by ID elsewhere. Only valid with '--format json'."
    )]
    public bool IsNormalized { get; set; } = false;

    [CommandOption(
        "cache",
        Description = "Remember guild members between runs, so that splitting an export into "
            + "several invocations doesn't re-resolve the same people every time. Only member "
            + "lookups are cached; everything else is always fetched fresh."
    )]
    public bool IsCacheEnabled { get; set; } = false;

    [CommandOption(
        "cache-ttl",
        Description = "How long a cached member stays usable, e.g. '7d', '12h', '90m'. "
            + "Nicknames, roles, and avatars do change, and a cached export records them as they "
            + "were, so keep this short unless you don't mind. Requires --cache.",
        Converter = typeof(TimeSpanInputConverter)
    )]
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromDays(1);

    [CommandOption(
        "cache-file",
        Description = "Path to the cache file. Defaults to 'cache.json' next to the executable. "
            + "Requires --cache.",
        EnvironmentVariable = "DISCORDCHATEXPORTER_CACHE_PATH"
    )]
    public string? CacheFilePath
    {
        get;
        set => field = value is not null ? Path.GetFullPath(value) : null;
    }

    [CommandOption(
        "skip-empty",
        Description = "Don't write a file for a channel that has no messages in the exported "
            + "range. By default such a channel still produces an empty file, which on a whole"
            + "-server export by date range can be most of the output."
    )]
    public bool ShouldSkipEmptyChannels { get; set; } = false;

    [CommandOption(
        "dateformat",
        Description = "This option doesn't do anything. Kept for backwards compatibility."
    )]
    public string DateFormat { get; set; } = "MM/dd/yyyy h:mm tt";

    [CommandOption(
        "locale",
        Description = "Locale to use when formatting dates and numbers. "
            + "If not specified, the default system locale will be used."
    )]
    public string? Locale { get; set; }

    [CommandOption("utc", Description = "Normalize all timestamps to UTC+0.")]
    public bool IsUtcNormalizationEnabled { get; set; } = false;

    [CommandOption(
        "fuck-russia",
        EnvironmentVariable = "FUCK_RUSSIA",
        Description = "Don't print the Support Ukraine message to the console.",
        // Use a converter to accept '1' as 'true' to reuse the existing environment variable
        Converter = typeof(TruthyBooleanInputConverter)
    )]
    public bool IsUkraineSupportMessageDisabled { get; set; } = false;

    // Lazily initialized, but deliberately not touched for the first time from inside the
    // parallel export loop: the '??=' is not atomic, so racing threads could each end up with a
    // separate exporter, and with it a separate cache.
    [field: AllowNull, MaybeNull]
    protected ChannelExporter Exporter =>
        field ??= new ChannelExporter(
            Discord,
            new ExportCache(
                Discord,
                IsCacheEnabled
                    ? new MemberCache(
                        CacheFilePath ?? Path.Combine(AppContext.BaseDirectory, "cache.json"),
                        CacheTtl,
                        Token
                    )
                    : null
            )
        );

    protected async ValueTask ExportAsync(IConsole console, IReadOnlyList<Channel> channels)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        // Asset reuse can only be enabled if the download assets option is set
        // https://github.com/Tyrrrz/DiscordChatExporter/issues/425
        if (ShouldReuseAssets && !ShouldDownloadAssets)
        {
            throw new CommandException("Option --reuse-media cannot be used without --media.");
        }

        // Assets directory can only be specified if the download assets option is set
        if (!string.IsNullOrWhiteSpace(AssetsDirPath) && !ShouldDownloadAssets)
        {
            throw new CommandException("Option --media-dir cannot be used without --media.");
        }

        // Cache options only mean something when the cache is on, mirroring --media/--media-dir
        if (!IsCacheEnabled && (CacheFilePath is not null || CacheTtl != TimeSpan.FromDays(1)))
        {
            throw new CommandException(
                "Options --cache-ttl and --cache-file cannot be used without --cache."
            );
        }

        // Normalization restructures the JSON schema, so it has no meaning for other formats
        if (IsNormalized && ExportFormat != ExportFormat.Json)
        {
            throw new CommandException("Option --normal can only be used with '--format json'.");
        }

        // Make sure the user does not try to export multiple channels into one file.
        // Output path must either be a directory or contain template tokens for this to work.
        // Validate this up-front, before fetching threads, because thread fetching can take a
        // long time and it's frustrating to fail only after it completes.
        // https://github.com/Tyrrrz/DiscordChatExporter/issues/799
        // https://github.com/Tyrrrz/DiscordChatExporter/issues/917
        // https://github.com/Tyrrrz/DiscordChatExporter/issues/1549
        var mayExportMultipleChannels =
            // Multiple channels were provided explicitly
            channels.Count > 1
            // Thread inclusion can add more channels to the export
            || ThreadInclusionMode != ThreadInclusionMode.None;

        var isValidOutputPath =
            // Anything is valid when exporting a single channel
            !mayExportMultipleChannels
            // When using template tokens, assume the user knows what they're doing
            || OutputPath.Contains('%')
            // Otherwise, require an existing directory or an unambiguous directory path
            || Directory.Exists(OutputPath)
            || Path.EndsInDirectorySeparator(OutputPath);

        if (!isValidOutputPath)
        {
            throw new CommandException(
                "Attempted to export multiple channels, but the output path is neither a directory nor a template. "
                    + "If the provided output path is meant to be treated as a directory, make sure it ends with a slash. "
                    + $"Provided output path: '{OutputPath}'."
            );
        }

        var unwrappedChannels = new List<Channel>(channels);

        // Unwrap threads
        if (ThreadInclusionMode != ThreadInclusionMode.None)
        {
            await console.Output.WriteLineAsync("Fetching threads...");

            var fetchedThreadsCount = 0;
            await console
                .CreateStatusTicker()
                .StartAsync(
                    "...",
                    async ctx =>
                    {
                        await foreach (
                            var thread in Discord.GetChannelThreadsAsync(
                                channels,
                                ThreadInclusionMode.ThreadKinds,
                                Before,
                                After,
                                cancellationToken
                            )
                        )
                        {
                            unwrappedChannels.Add(thread);

                            ctx.Status(Markup.Escape($"Fetched '{thread.GetHierarchicalName()}'."));

                            fetchedThreadsCount++;
                        }
                    }
                );

            if (ThreadInclusionMode.IncludesParentChannels)
            {
                // Remove forums, as they cannot be exported directly and their constituent threads
                // have already been fetched.
                unwrappedChannels.RemoveAll(channel => channel.Kind == ChannelKind.GuildForum);
            }
            else
            {
                // Drop everything that isn't a thread, which covers forums as well. The channels
                // had to be resolved anyway to find the threads hanging off them.
                unwrappedChannels.RemoveAll(channel => !channel.IsThread);
            }

            await console.Output.WriteLineAsync($"Fetched {fetchedThreadsCount} thread(s).");
        }

        // Resolve the exporter up-front so that every channel task shares one instance, and with
        // it one cache of guild data
        var exporter = Exporter;

        if (IsCacheEnabled)
            await exporter.Cache.LoadPersistedDataAsync(cancellationToken);

        // Export
        var errorsByChannel = new ConcurrentDictionary<Channel, string>();
        var warningsByChannel = new ConcurrentDictionary<Channel, string>();

        await console.Output.WriteLineAsync($"Exporting {unwrappedChannels.Count} channel(s)...");

        try
        {
            // Owns the per-channel counters that the progress display reads
            var statsColumn = new ExportStatsColumn();

            await console
                .CreateProgressTicker(statsColumn)
                .HideCompleted(
                    // When exporting multiple channels in parallel, hide the completed tasks
                    // because it gets hard to visually parse them as they complete out of order.
                    // https://github.com/Tyrrrz/DiscordChatExporter/issues/1124
                    ParallelLimit > 1
                )
                .StartAsync(async ctx =>
                {
                    await Parallel.ForEachAsync(
                        unwrappedChannels,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Math.Max(1, ParallelLimit),
                            CancellationToken = cancellationToken,
                        },
                        async (channel, innerCancellationToken) =>
                        {
                            // One set of counters per channel, published to the ambient slot so
                            // that the HTTP handler and the writer can both find it. Each
                            // iteration of the parallel loop gets its own async context, which is
                            // what keeps these from bleeding into one another.
                            var stats = new ExportStats
                            {
                                // Only meaningful without a date range, since Discord's count
                                // covers the whole channel rather than the exported window
                                TotalMessages =
                                    After is null && Before is null ? channel.MessageCount : null,
                            };

                            ExportStats.Current = stats;

                            try
                            {
                                await ctx.StartTaskAsync(
                                    Markup.Escape(channel.GetHierarchicalName()),
                                    task => statsColumn.Attach(task, stats),
                                    async progress =>
                                    {
                                        // Resolved through the cache, so channels that share a
                                        // guild don't each re-fetch it. Deliberately kept inside the
                                        // loop: a guild that fails to resolve is reported per channel
                                        // and the rest of the export continues.
                                        var guild = await exporter.Cache.GetGuildAsync(
                                            channel.GuildId,
                                            innerCancellationToken
                                        );

                                        var request = new ExportRequest(
                                            guild,
                                            channel,
                                            OutputPath,
                                            AssetsDirPath,
                                            ExportFormat,
                                            After,
                                            Before,
                                            PartitionLimit,
                                            MessageFilter,
                                            IsReverseMessageOrder,
                                            ShouldFormatMarkdown,
                                            ShouldDownloadAssets,
                                            ShouldReuseAssets,
                                            IsNormalized,
                                            IsExtended,
                                            ShouldFetchReactionUsers,
                                            IsCacheEnabled,
                                            ShouldSkipEmptyChannels,
                                            Locale,
                                            IsUtcNormalizationEnabled
                                        );

                                        await exporter.ExportChannelAsync(
                                            request,
                                            progress.ToPercentageBased(),
                                            innerCancellationToken
                                        );
                                    }
                                );
                            }
                            catch (ChannelEmptyException ex)
                            {
                                warningsByChannel[channel] = ex.Message;
                            }
                            catch (DiscordChatExporterException ex) when (!ex.IsFatal)
                            {
                                errorsByChannel[channel] = ex.Message;
                            }
                        }
                    );
                });
        }
        finally
        {
            // Persist even when the run was cut short, so that a cancelled export still leaves
            // the next one less work to do. Deliberately not cancellable for the same reason.
            if (IsCacheEnabled)
                await exporter.Cache.SavePersistedDataAsync(CancellationToken.None);
        }

        // Print the result
        using (console.WithForegroundColor(ConsoleColor.White))
        {
            await console.Output.WriteLineAsync(
                $"Successfully exported {unwrappedChannels.Count - errorsByChannel.Count} channel(s)."
            );
        }

        // Print the request breakdown, when tracing is enabled
        if (HttpRequestTracer.IsEnabled && Http.Tracer is { } tracer)
        {
            await console.Error.WriteLineAsync();
            await console.Error.WriteLineAsync("API requests by route:");

            foreach (var (route, count) in tracer.Counts)
                await console.Error.WriteLineAsync($"{count, 8}  {route}");

            await console.Error.WriteLineAsync($"{tracer.TotalCount, 8}  TOTAL");

            if (tracer.RateLimitedCount > 0)
            {
                await console.Error.WriteLineAsync(
                    $"{tracer.RateLimitedCount, 8}  ...of which were rate-limited and retried"
                );
            }

            await console.Error.WriteLineAsync();
        }

        // Print warnings
        if (warningsByChannel.Any())
        {
            await console.Output.WriteLineAsync();

            using (console.WithForegroundColor(ConsoleColor.Yellow))
            {
                await console.Error.WriteLineAsync(
                    "Warnings reported for the following channel(s):"
                );
            }

            foreach (var (channel, message) in warningsByChannel)
            {
                await console.Error.WriteAsync($"{channel.GetHierarchicalName()}: ");
                using (console.WithForegroundColor(ConsoleColor.Yellow))
                    await console.Error.WriteLineAsync(message);
            }

            await console.Error.WriteLineAsync();
        }

        // Print errors
        if (errorsByChannel.Any())
        {
            await console.Output.WriteLineAsync();

            using (console.WithForegroundColor(ConsoleColor.Red))
            {
                await console.Error.WriteLineAsync("Failed to export the following channel(s):");
            }

            foreach (var (channel, message) in errorsByChannel)
            {
                await console.Error.WriteAsync($"{channel.GetHierarchicalName()}: ");
                using (console.WithForegroundColor(ConsoleColor.Red))
                    await console.Error.WriteLineAsync(message);
            }

            await console.Error.WriteLineAsync();
        }

        // Fail the command only if ALL channels failed to export.
        // If only some channels failed to export, it's okay.
        if (errorsByChannel.Count >= unwrappedChannels.Count)
            throw new CommandException("Export failed.");
    }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        // Support Ukraine callout
        if (!IsUkraineSupportMessageDisabled)
        {
            console.Output.WriteLine(
                "┌────────────────────────────────────────────────────────────────────┐"
            );
            console.Output.WriteLine(
                "│   Thank you for supporting Ukraine <3                              │"
            );
            console.Output.WriteLine(
                "│                                                                    │"
            );
            console.Output.WriteLine(
                "│   As Russia wages a genocidal war against my country,              │"
            );
            console.Output.WriteLine(
                "│   I'm grateful to everyone who continues to                        │"
            );
            console.Output.WriteLine(
                "│   stand with Ukraine in our fight for freedom.                     │"
            );
            console.Output.WriteLine(
                "│                                                                    │"
            );
            console.Output.WriteLine(
                "│   Learn more: https://tyrrrz.me/ukraine                            │"
            );
            console.Output.WriteLine(
                "└────────────────────────────────────────────────────────────────────┘"
            );
            console.Output.WriteLine("");
        }

        await base.ExecuteAsync(console);
    }
}
