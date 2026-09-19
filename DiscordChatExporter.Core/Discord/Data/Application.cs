using System.Text.Json;
using JsonExtensions.Reading;
using PowerKit.Extensions;

namespace DiscordChatExporter.Core.Discord.Data;

// https://discord.com/developers/docs/resources/application#application-object
public partial record Application(Snowflake Id, string Name, ApplicationFlags Flags)
{
    public bool IsMessageContentIntentEnabled { get; } =
        Flags.HasFlag(ApplicationFlags.GatewayMessageContent)
        || Flags.HasFlag(ApplicationFlags.GatewayMessageContentLimited);

    // Required to walk the member list. The 'Limited' variant is what an unverified bot gets,
    // and it grants the same access, so either one will do.
    public bool IsGuildMembersIntentEnabled { get; } =
        Flags.HasFlag(ApplicationFlags.GatewayGuildMembers)
        || Flags.HasFlag(ApplicationFlags.GatewayGuildMembersLimited);
}

public partial record Application
{
    public static Application Parse(JsonElement json)
    {
        var id = json.GetProperty("id").GetNonWhiteSpaceString().Pipe(Snowflake.Parse);
        var name = json.GetProperty("name").GetNonWhiteSpaceString();

        var flags =
            json.GetPropertyOrNull("flags")?.GetInt32OrNull()?.Pipe(x => (ApplicationFlags)x)
            ?? ApplicationFlags.None;

        return new Application(id, name, flags);
    }
}
