using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Utils;
using JsonExtensions.Writing;
using PowerKit.Extensions;

namespace DiscordChatExporter.Core.Exporting;

// Writes out a guild's roster. This deliberately sits outside the channel export pipeline: there
// are no messages involved, so none of the context, asset or partitioning machinery applies.
//
// The document keeps users and members apart, which a message export does not: there, the author
// object is a merge of the two (the nickname, colour and roles on it all come from the member).
// Here a 'member' holds only what is true inside this one guild, and the 'user' it points at holds
// the global identity that would look the same in any other guild.
public partial class MemberExporter(DiscordClient discord)
{
    public async ValueTask<int> ExportAsync(
        Guild guild,
        string outputFilePath,
        bool isNormalized = false,
        bool shouldResolveFullUsers = false,
        bool isUtcNormalizationEnabled = false,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        DateTimeOffset NormalizeDate(DateTimeOffset instant) =>
            isUtcNormalizationEnabled ? instant.ToUniversalTime() : instant.ToLocalTime();

        var dirPath = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrWhiteSpace(dirPath))
            Directory.CreateDirectory(dirPath);

        await using var stream = File.Create(outputFilePath);
        await using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                // https://github.com/Tyrrrz/DiscordChatExporter/issues/450
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true,
                // Validation errors may mask actual failures
                // https://github.com/Tyrrrz/DiscordChatExporter/issues/413
                SkipValidation = true,
            }
        );

        // A member's own payload carries role IDs only, so the full objects are taken from the
        // guild, which already holds its entire role inventory
        var rolesById = guild.Roles.ToDictionary(r => r.Id);

        // Root object (start)
        writer.WriteStartObject();

        // Modifications made by this fork, so that parsers can detect them up-front. There is no
        // 'extended' counterpart here, because this whole document is an addition to begin with.
        writer.WriteStartObject("mod");
        writer.WriteBoolean("normal", isNormalized);
        // Provenance: without this, a null 'bannerUrl' on a user means "not known" rather than
        // "not set", because the roster payload doesn't carry the field at all
        writer.WriteBoolean("fullUsers", shouldResolveFullUsers);
        writer.WriteEndObject();

        WriteGuild(writer, guild, isNormalized);

        writer.WriteString("exportedAt", NormalizeDate(DateTimeOffset.UtcNow));

        // Under --normal the users are collected as the roster streams past and written out as a
        // table at the end, mirroring how a message export defers its lookup tables
        var users = new List<User>();

        writer.WriteStartArray("members");

        var count = 0;

        await foreach (var member in discord.GetGuildMembersAsync(guild.Id, cancellationToken))
        {
            // Same ordering a client renders: highest role first. Discord leaves @everyone out
            // of a member's role list, so it is absent here too.
            var roles = member
                .RoleIds.Select(rolesById.GetValueOrDefault)
                .WhereNotNull()
                .OrderByDescending(r => r.Position)
                .ToArray();

            // The user nested in a roster entry is a partial one: Discord sends 'banner' as null
            // on it regardless of whether the account has one, so a global banner looks absent
            // for everybody. Only a dedicated user fetch carries it, which is why filling it in
            // is opt-in -- it costs one request per member.
            var user = shouldResolveFullUsers
                ? await discord.TryGetUserAsync(member.Id, cancellationToken) ?? member.User
                : member.User;

            writer.WriteStartObject();

            // A member has no identity of its own: it is this user's profile within this guild
            writer.WriteString("userId", member.Id.ToString());

            // The guild-specific nickname, null when the member never set one
            writer.WriteString("nickname", member.DisplayName);

            // What a client actually renders: the nickname if there is one, and otherwise the
            // user's own display name. Provided for convenience; it is the one field here that
            // mixes the two layers.
            writer.WriteString("displayName", member.DisplayName ?? user.DisplayName);

            // Discord colours a name after the highest-positioned role that carries a colour
            writer.WriteString(
                "color",
                roles.FirstOrDefault(r => r.Color is not null)?.Color?.ToHexString()
            );

            // Guild-specific overrides only, so null means "wears the global one", which is on
            // the user object rather than duplicated here
            writer.WriteString("avatarUrl", member.AvatarUrl);
            writer.WriteString("bannerUrl", member.BannerUrl);

            writer.WriteString("joinedAt", member.JoinedAt?.Pipe(NormalizeDate));
            writer.WriteString("premiumSince", member.PremiumSince?.Pipe(NormalizeDate));
            writer.WriteBoolean("isPending", member.IsPending);

            // Decomposed rather than written as a bitfield, so that a reader doesn't need to know
            // the bit values. Bits Discord has added since are kept as their numeric value.
            WriteReferenceArray(writer, "flags", EnumUtils.GetFlagNames(member.Flags));

            if (isNormalized)
            {
                WriteReferenceArray(writer, "roleIds", roles.Select(r => r.Id.ToString()));
                users.Add(user);
            }
            else
            {
                writer.WriteStartArray("roles");

                foreach (var role in roles)
                    WriteRole(writer, role);

                writer.WriteEndArray();

                writer.WritePropertyName("user");
                WriteUser(writer, user);
            }

            writer.WriteEndObject();

            count++;
            progress?.Report(count);

            // Keep the buffer from growing with the roster
            if (count % 100 == 0)
                await writer.FlushAsync(cancellationToken);
        }

        writer.WriteEndArray();

        if (isNormalized)
        {
            writer.WriteStartArray("users");

            foreach (var user in users)
                WriteUser(writer, user);

            writer.WriteEndArray();

            // The whole inventory, not just the roles somebody happens to wear
            writer.WriteStartArray("roles");

            foreach (var role in guild.Roles)
                WriteRole(writer, role);

            writer.WriteEndArray();
        }

        writer.WriteNumber("memberCount", count);

        // Root object (end)
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);

        return count;
    }

    private static void WriteGuild(Utf8JsonWriter writer, Guild guild, bool isNormalized)
    {
        writer.WriteStartObject("guild");

        writer.WriteString("id", guild.Id.ToString());
        writer.WriteString("name", guild.Name);
        writer.WriteString("iconUrl", guild.IconUrl);
        writer.WriteString("description", guild.Description);
        writer.WriteString("vanityUrl", guild.VanityUrl);
        writer.WriteString("bannerUrl", guild.BannerUrl);
        writer.WriteString("splashUrl", guild.SplashUrl);
        writer.WriteString("ownerId", guild.OwnerId?.ToString());
        writer.WriteNumber("premiumTier", guild.PremiumTier);
        writer.WriteNumber("premiumSubscriptionCount", guild.PremiumSubscriptionCount);

        // Discord's own estimate, which is worth keeping next to the exact count at the bottom
        // of the document: the two disagreeing is normal, not a sign of a truncated export
        writer.WriteNumber("approximateMemberCount", guild.ApproximateMemberCount);
        writer.WriteNumber("approximatePresenceCount", guild.ApproximatePresenceCount);

        // Emoji and stickers are left out on purpose: this document is about people
        if (isNormalized)
        {
            WriteReferenceArray(writer, "roleIds", guild.Roles.Select(r => r.Id.ToString()));
        }
        else
        {
            writer.WriteStartArray("roles");

            foreach (var role in guild.Roles)
                WriteRole(writer, role);

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    // The user as Discord knows them globally, with nothing guild-specific mixed in. Whether
    // 'bannerUrl' is meaningful depends on mod.fullUsers, since the roster's own copy of the user
    // never carries a banner.
    private static void WriteUser(Utf8JsonWriter writer, User user)
    {
        writer.WriteStartObject();

        writer.WriteString("id", user.Id.ToString());
        writer.WriteString("name", user.Name);
        writer.WriteString("discriminator", user.DiscriminatorFormatted);
        writer.WriteString("displayName", user.DisplayName);
        writer.WriteBoolean("isBot", user.IsBot);
        writer.WriteString("avatarUrl", user.AvatarUrl);
        writer.WriteString("bannerUrl", user.BannerUrl);

        writer.WriteEndObject();
    }

    private static void WriteRole(Utf8JsonWriter writer, Role role)
    {
        writer.WriteStartObject();

        writer.WriteString("id", role.Id.ToString());
        writer.WriteString("name", role.Name);
        writer.WriteString("color", role.Color?.ToHexString());
        writer.WriteNumber("position", role.Position);

        writer.WriteEndObject();
    }

    private static void WriteReferenceArray(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<string> references
    )
    {
        writer.WriteStartArray(propertyName);

        foreach (var reference in references)
            writer.WriteStringValue(reference);

        writer.WriteEndArray();
    }
}

public partial class MemberExporter
{
    public static string GetDefaultOutputFileName(Guild guild) =>
        Path.EscapeFileName($"{guild.Name} [{guild.Id}] - Members.json");

    private static string FormatPath(string path, Guild guild) =>
        Regex.Replace(
            path,
            "%.",
            m =>
                Path.EscapeFileName(
                    m.Value switch
                    {
                        "%g" => guild.Id.ToString(),
                        "%G" => guild.Name,
                        "%d" => DateTimeOffset.Now.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture
                        ),
                        "%%" => "%",
                        _ => m.Value,
                    }
                )
        );

    public static string GetOutputFilePath(Guild guild, string outputPath)
    {
        var actualOutputPath = FormatPath(outputPath, guild);

        // Same heuristic as a channel export, including the reason it reads the extension off
        // the original path: a substituted guild name may contain a period, which would
        // otherwise turn a directory into a file.
        // https://github.com/Tyrrrz/DiscordChatExporter/issues/1502
        if (
            Directory.Exists(actualOutputPath)
            || string.IsNullOrWhiteSpace(Path.GetExtension(outputPath))
        )
        {
            return Path.Combine(actualOutputPath, GetDefaultOutputFileName(guild));
        }

        return actualOutputPath;
    }
}
