using System;
using System.IO;
using System.Threading.Tasks;
using CliFx.Binding;
using CliFx.Infrastructure;
using DiscordChatExporter.Cli.Commands.Base;
using DiscordChatExporter.Cli.Utils.Extensions;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Exporting;
using Spectre.Console;

namespace DiscordChatExporter.Cli.Commands;

[Command(
    "exportusers",
    Description = "Export the member list of a server, along with the users behind it."
)]
public partial class ExportUsersCommand : DiscordCommandBase
{
    [CommandOption("guild", 'g', Description = "Server ID.")]
    public required Snowflake GuildId { get; set; }

    [CommandOption(
        "output",
        'o',
        Description = "Output file or directory path. "
            + "If a directory is specified, the file name will be generated automatically based on the server name. "
            + "Directory paths must end with a slash to avoid ambiguity. "
            + "Supports the %g, %G, %d and %% template tokens."
    )]
    public string OutputPath
    {
        get;
        // Handle ~/ in paths on Unix systems
        // https://github.com/Tyrrrz/DiscordChatExporter/pull/903
        set => field = Path.GetFullPath(value);
    } = Directory.GetCurrentDirectory();

    [CommandOption(
        "normal",
        Description = "Normalize the output: users and roles are emitted once into lookup tables "
            + "at the root of the document, and referenced by ID elsewhere."
    )]
    public bool IsNormalized { get; set; } = false;

    [CommandOption(
        "full-users",
        Description = "Fetch each user's full profile, which is the only way to get their global "
            + "banner: the member list itself does not carry one. Costs one extra request per "
            + "member, so it turns a handful of requests into thousands."
    )]
    public bool ShouldResolveFullUsers { get; set; } = false;

    [CommandOption("utc", Description = "Normalize all timestamps to UTC+0.")]
    public bool IsUtcNormalizationEnabled { get; set; } = false;

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        await base.ExecuteAsync(console);

        var cancellationToken = console.RegisterCancellationHandler();

        var guild = await Discord.GetGuildAsync(GuildId, cancellationToken);
        var filePath = MemberExporter.GetOutputFilePath(guild, OutputPath);

        // Only an estimate, and it can sit either side of the real roster, so the percentage is
        // capped on the way up and the ticker is finished off by StartTaskAsync on the way out
        var estimatedCount = guild.ApproximateMemberCount ?? 0;

        var exportedCount = 0;

        await console
            .CreateProgressTicker()
            .StartAsync(async ctx =>
            {
                await ctx.StartTaskAsync(
                    Markup.Escape(guild.Name),
                    null,
                    async progress =>
                    {
                        exportedCount = await new MemberExporter(Discord).ExportAsync(
                            guild,
                            filePath,
                            IsNormalized,
                            ShouldResolveFullUsers,
                            IsUtcNormalizationEnabled,
                            new Progress<int>(count =>
                            {
                                if (estimatedCount > 0)
                                    progress.Value = Math.Min(1.0, (double)count / estimatedCount);
                            }),
                            cancellationToken
                        );
                    }
                );
            });

        await console.Output.WriteLineAsync($"Exported {exportedCount} member(s) to '{filePath}'.");
    }
}
