using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Discord.Data.Embeds;
using DiscordChatExporter.Core.Markdown.Parsing;
using DiscordChatExporter.Core.Utils;
using JsonExtensions.Writing;
using PowerKit.Extensions;

namespace DiscordChatExporter.Core.Exporting;

internal class JsonMessageWriter(Stream stream, ExportContext context)
    : MessageWriter(stream, context)
{
    private readonly Utf8JsonWriter _writer = new(
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

    private readonly bool _isNormalized = context.Request.IsNormalized;

    private readonly bool _shouldFetchReactionUsers = context.Request.ShouldFetchReactionUsers;

    // Gates every field this fork adds on top of the original DCE schema, so that a default
    // export stays readable by anything written against vanilla DiscordChatExporter.
    private readonly bool _isExtended = context.Request.IsExtended;

    // In normalized mode, entities that have an identity are written to lookup tables at the root
    // of the document instead of being repeated inline at every occurrence. They are collected
    // while the messages are streamed out and flushed in the postamble, which is also why the
    // tables are rendered as late as possible: member info is resolved on demand as the export
    // progresses, so deferring gives every entry the richest data the export ever saw.
    private readonly Dictionary<Snowflake, User> _users = [];
    private readonly Dictionary<Snowflake, Role> _roles = [];
    private readonly Dictionary<Snowflake, Sticker> _stickers = [];
    private readonly Dictionary<string, Emoji> _emojis = new(StringComparer.Ordinal);
    private readonly Dictionary<Emoji, string> _emojiKeys = [];

    private async ValueTask<string> FormatMarkdownAsync(
        string markdown,
        CancellationToken cancellationToken = default
    ) =>
        Context.Request.ShouldFormatMarkdown
            ? await PlainTextMarkdownVisitor.FormatAsync(Context, markdown, cancellationToken)
            : markdown;

    private string RegisterUser(User user)
    {
        // Keep the first occurrence; the role list, nickname, color, and avatar of a table entry
        // are resolved from the context at render time rather than from the instance stored here.
        _users.TryAdd(user.Id, user);
        return user.Id.ToString();
    }

    private string RegisterSticker(Sticker sticker)
    {
        _stickers.TryAdd(sticker.Id, sticker);
        return sticker.Id.ToString();
    }

    private string RegisterEmoji(Emoji emoji)
    {
        // Emoji is a record, so this matches on the full identity (ID, name, animated flag)
        if (_emojiKeys.TryGetValue(emoji, out var existingKey))
            return existingKey;

        // Custom emoji are identified by their ID. Standard emoji don't have one, so they are
        // identified by their name, which for them is the actual character (e.g., 🙂). The two
        // key spaces cannot overlap, because a snowflake is never a valid emoji character.
        var baseKey = emoji.Id?.ToString() ?? emoji.Name;

        // A custom emoji can be renamed over its lifetime, while older messages keep referencing
        // the name it had back then. Those are distinct records, so give them distinct keys
        // instead of collapsing them and losing one of the names.
        var key = baseKey;
        for (var i = 2; _emojis.ContainsKey(key); i++)
            key = $"{baseKey}~{i}";

        _emojiKeys[emoji] = key;
        _emojis[key] = emoji;

        return key;
    }

    private void WriteReferenceArray(string propertyName, IEnumerable<string> references)
    {
        _writer.WriteStartArray(propertyName);

        foreach (var reference in references)
            _writer.WriteStringValue(reference);

        _writer.WriteEndArray();
    }

    private async ValueTask WriteUserAsync(
        User user,
        bool includeRoles = true,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartObject();

        _writer.WriteString("id", user.Id.ToString());
        _writer.WriteString("name", user.Name);
        _writer.WriteString("discriminator", user.DiscriminatorFormatted);

        // Guild-specific member data. It is null for users who are not (or are no longer) members
        // of the guild, which includes users seen only through reactions.
        var member = Context.TryGetMember(user.Id);

        _writer.WriteString("nickname", member?.DisplayName ?? user.DisplayName);

        _writer.WriteString("color", Context.TryGetUserColor(user.Id)?.ToHexString());
        _writer.WriteBoolean("isBot", user.IsBot);

        if (_isExtended)
        {
            _writer.WriteString("joinedAt", member?.JoinedAt?.Pipe(Context.NormalizeDate));
            _writer.WriteString("premiumSince", member?.PremiumSince?.Pipe(Context.NormalizeDate));
            _writer.WriteBoolean("isPending", member?.IsPending ?? false);

            // Decomposed rather than written as a bitfield, so that a reader doesn't need to know
            // the bit values. Bits Discord has added since are kept as their numeric value.
            WriteReferenceArray("flags", GetFlagNames(member?.Flags ?? MemberFlags.None));
        }

        if (includeRoles)
        {
            var roles = Context.GetUserRoles(user.Id);

            if (_isNormalized)
            {
                // Role order is significant (descending by position), so it's preserved here
                WriteReferenceArray(
                    "roleIds",
                    roles.Select(r =>
                    {
                        _roles.TryAdd(r.Id, r);
                        return r.Id.ToString();
                    })
                );
            }
            else
            {
                _writer.WritePropertyName("roles");
                await WriteRolesAsync(roles, cancellationToken);
            }
        }

        _writer.WriteString(
            "avatarUrl",
            await Context.ResolveAssetUrlAsync(
                member?.AvatarUrl ?? user.AvatarUrl,
                cancellationToken
            )
        );

        if (_isExtended)
        {
            // Same precedence as the avatar: a guild-specific banner wins over the global one.
            // Unlike the avatar there is no fallback, so this stays null when neither is set.
            //
            // The member's own copy of the user is consulted before the one we were handed,
            // because the latter often comes from a message payload, where Discord sends only a
            // partial user object with no banner on it at all.
            var bannerUrl = member?.BannerUrl ?? member?.User.BannerUrl ?? user.BannerUrl;

            _writer.WriteString(
                "bannerUrl",
                bannerUrl is not null
                    ? await Context.ResolveAssetUrlAsync(bannerUrl, cancellationToken)
                    : null
            );
        }

        _writer.WriteEndObject();
        await _writer.FlushAsync(cancellationToken);
    }

    // Shared with the member export, which writes the same flag arrays
    private static IEnumerable<string> GetFlagNames<T>(T flags)
        where T : struct, Enum => EnumUtils.GetFlagNames(flags);

    private static string ToCamelCase(string name)
    {
        if (!name.Contains('_', StringComparison.Ordinal))
            return name;

        var buffer = new StringBuilder(name.Length);
        var shouldCapitalize = false;

        foreach (var c in name)
        {
            if (c == '_')
            {
                shouldCapitalize = true;
                continue;
            }

            buffer.Append(shouldCapitalize ? char.ToUpperInvariant(c) : c);
            shouldCapitalize = false;
        }

        return buffer.ToString();
    }

    // The component tree is mirrored rather than projected, for the reason given on the Component
    // record: only the property names are changed, from the API's snake_case to the camelCase used
    // by every other key in this document. That transform is mechanical and reversible, so the
    // result can still be read against Discord's own component documentation.
    private async ValueTask WriteComponentJsonAsync(
        JsonElement json,
        bool isMedia = false,
        CancellationToken cancellationToken = default
    )
    {
        switch (json.ValueKind)
        {
            case JsonValueKind.Object:
                _writer.WriteStartObject();

                foreach (var property in json.EnumerateObject())
                {
                    _writer.WritePropertyName(ToCamelCase(property.Name));

                    // Media referenced by a component lives on the CDN behind a signed URL that
                    // expires within the day, so it has to go through the asset pipeline like an
                    // attachment does. Only media objects are treated this way: a link button also
                    // carries a 'url', but that one points at an arbitrary site rather than at a
                    // downloadable asset.
                    if (
                        isMedia
                        && property.Value.ValueKind is JsonValueKind.String
                        && property.Name is "url" or "proxy_url"
                    )
                    {
                        _writer.WriteStringValue(
                            await Context.ResolveAssetUrlAsync(
                                property.Value.GetString() ?? "",
                                cancellationToken
                            )
                        );
                    }
                    else
                    {
                        await WriteComponentJsonAsync(
                            property.Value,
                            property.NameEquals("media") || property.NameEquals("file"),
                            cancellationToken
                        );
                    }
                }

                _writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                _writer.WriteStartArray();

                foreach (var item in json.EnumerateArray())
                    await WriteComponentJsonAsync(item, isMedia, cancellationToken);

                _writer.WriteEndArray();
                break;

            default:
                json.WriteTo(_writer);
                break;
        }
    }

    private async ValueTask WriteComponentsAsync(
        IReadOnlyList<Component> components,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartArray("components");

        foreach (var component in components)
            await WriteComponentJsonAsync(component.Json, cancellationToken: cancellationToken);

        _writer.WriteEndArray();
        await _writer.FlushAsync(cancellationToken);
    }

    private async ValueTask WriteEmojiAsync(
        Emoji emoji,
        string? key = null,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartObject();

        // Only present on table entries, where it's the value that messages reference
        if (key is not null)
            _writer.WriteString("key", key);

        _writer.WriteString("id", emoji.Id.ToString());
        _writer.WriteString("name", emoji.Name);
        _writer.WriteString("code", emoji.Code);
        _writer.WriteBoolean("isAnimated", emoji.IsAnimated);
        _writer.WriteString(
            "imageUrl",
            await Context.ResolveAssetUrlAsync(emoji.ImageUrl, cancellationToken)
        );

        _writer.WriteEndObject();
        await _writer.FlushAsync(cancellationToken);
    }

    private async ValueTask WriteRolesAsync(
        IReadOnlyList<Role> roles,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartArray();

        foreach (var role in roles)
        {
            _writer.WriteStartObject();

            _writer.WriteString("id", role.Id.ToString());
            _writer.WriteString("name", role.Name);
            _writer.WriteString("color", role.Color?.ToHexString());
            _writer.WriteNumber("position", role.Position);

            _writer.WriteEndObject();
        }

        _writer.WriteEndArray();
        await _writer.FlushAsync(cancellationToken);
    }

    private async ValueTask WriteAttachmentAsync(
        Attachment attachment,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartObject();

        _writer.WriteString("id", attachment.Id.ToString());
        _writer.WriteString(
            "url",
            await Context.ResolveAssetUrlAsync(attachment.Url, cancellationToken)
        );
        _writer.WriteString("fileName", attachment.FileName);
        _writer.WriteNumber("fileSizeBytes", attachment.FileSize.TotalBytes);

        _writer.WriteEndObject();
    }

    private async ValueTask WriteEmbedAuthorAsync(
        EmbedAuthor embedAuthor,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartObject();

        _writer.WriteString("name", embedAuthor.Name);
        _writer.WriteString("url", embedAuthor.Url);

        if (!string.IsNullOrWhiteSpace(embedAuthor.IconUrl))
        {
            _writer.WriteString(
                "iconUrl",
                await Context.ResolveAssetUrlAsync(
                    embedAuthor.IconProxyUrl ?? embedAuthor.IconUrl,
                    cancellationToken
                )
            );

            _writer.WriteString("iconCanonicalUrl", embedAuthor.IconUrl);
        }

        _writer.WriteEndObject();
        await _writer.FlushAsync(cancellationToken);
    }

    private async ValueTask WriteEmbedImageAsync(
        EmbedImage embedImage,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartObject();

        if (!string.IsNullOrWhiteSpace(embedImage.Url))
        {
            _writer.WriteString(
                "url",
                await Context.ResolveAssetUrlAsync(
                    embedImage.ProxyUrl ?? embedImage.Url,
                    cancellationToken
                )
            );

            _writer.WriteString("canonicalUrl", embedImage.Url);
        }

        _writer.WriteNumber("width", embedImage.Width);
        _writer.WriteNumber("height", embedImage.Height);

        _writer.WriteEndObject();
        await _writer.FlushAsync(cancellationToken);
    }

    private async ValueTask WriteEmbedVideoAsync(
        EmbedVideo embedVideo,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartObject();

        if (!string.IsNullOrWhiteSpace(embedVideo.Url))
        {
            _writer.WriteString(
                "url",
                await Context.ResolveAssetUrlAsync(
                    embedVideo.ProxyUrl ?? embedVideo.Url,
                    cancellationToken
                )
            );

            _writer.WriteString("canonicalUrl", embedVideo.Url);
        }

        _writer.WriteNumber("width", embedVideo.Width);
        _writer.WriteNumber("height", embedVideo.Height);

        _writer.WriteEndObject();
        await _writer.FlushAsync(cancellationToken);
    }

    private async ValueTask WriteEmbedFooterAsync(
        EmbedFooter embedFooter,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartObject();

        _writer.WriteString("text", embedFooter.Text);

        if (!string.IsNullOrWhiteSpace(embedFooter.IconUrl))
        {
            _writer.WriteString(
                "iconUrl",
                await Context.ResolveAssetUrlAsync(
                    embedFooter.IconProxyUrl ?? embedFooter.IconUrl,
                    cancellationToken
                )
            );

            _writer.WriteString("iconCanonicalUrl", embedFooter.IconUrl);
        }

        _writer.WriteEndObject();
        await _writer.FlushAsync(cancellationToken);
    }

    private async ValueTask WriteEmbedFieldAsync(
        EmbedField embedField,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartObject();

        _writer.WriteString("name", await FormatMarkdownAsync(embedField.Name, cancellationToken));
        _writer.WriteString(
            "value",
            await FormatMarkdownAsync(embedField.Value, cancellationToken)
        );
        _writer.WriteBoolean("isInline", embedField.IsInline);

        _writer.WriteEndObject();
        await _writer.FlushAsync(cancellationToken);
    }

    private async ValueTask WriteEmbedAsync(
        Embed embed,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartObject();

        _writer.WriteString(
            "title",
            await FormatMarkdownAsync(embed.Title ?? "", cancellationToken)
        );
        _writer.WriteString("url", embed.Url);
        _writer.WriteString("timestamp", embed.Timestamp?.Pipe(Context.NormalizeDate));
        _writer.WriteString(
            "description",
            await FormatMarkdownAsync(embed.Description ?? "", cancellationToken)
        );

        if (embed.Color is not null)
            _writer.WriteString("color", embed.Color.Value.ToHexString());

        if (embed.Author is not null)
        {
            _writer.WritePropertyName("author");
            await WriteEmbedAuthorAsync(embed.Author, cancellationToken);
        }

        if (embed.Thumbnail is not null)
        {
            _writer.WritePropertyName("thumbnail");
            await WriteEmbedImageAsync(embed.Thumbnail, cancellationToken);
        }

        if (embed.Image is not null)
        {
            _writer.WritePropertyName("image");
            await WriteEmbedImageAsync(embed.Image, cancellationToken);
        }

        if (embed.Video is not null)
        {
            _writer.WritePropertyName("video");
            await WriteEmbedVideoAsync(embed.Video, cancellationToken);
        }

        if (embed.Footer is not null)
        {
            _writer.WritePropertyName("footer");
            await WriteEmbedFooterAsync(embed.Footer, cancellationToken);
        }

        // Images
        _writer.WriteStartArray("images");

        foreach (var image in embed.Images)
            await WriteEmbedImageAsync(image, cancellationToken);

        _writer.WriteEndArray();

        // Fields
        _writer.WriteStartArray("fields");

        foreach (var field in embed.Fields)
            await WriteEmbedFieldAsync(field, cancellationToken);

        _writer.WriteEndArray();

        // Inline emoji
        IEnumerable<Emoji> inlineEmojis = !string.IsNullOrWhiteSpace(embed.Description)
            ? MarkdownParser
                .ExtractEmojis(embed.Description)
                .DistinctBy(e => e.Name, StringComparer.Ordinal)
                .Select(e => new Emoji(e.Id, e.Name, e.IsAnimated))
            : [];

        if (_isNormalized)
        {
            WriteReferenceArray("inlineEmojiKeys", inlineEmojis.Select(RegisterEmoji));
        }
        else
        {
            _writer.WriteStartArray("inlineEmojis");

            foreach (var emoji in inlineEmojis)
                await WriteEmojiAsync(emoji, cancellationToken: cancellationToken);

            _writer.WriteEndArray();
        }

        _writer.WriteEndObject();
        await _writer.FlushAsync(cancellationToken);
    }

    private async ValueTask WriteStickerAsync(
        Sticker sticker,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteStartObject();

        _writer.WriteString("id", sticker.Id.ToString());
        _writer.WriteString("name", sticker.Name);
        _writer.WriteString("format", sticker.Format.ToString());
        _writer.WriteString(
            "sourceUrl",
            await Context.ResolveAssetUrlAsync(sticker.SourceUrl, cancellationToken)
        );

        _writer.WriteEndObject();
    }

    private async ValueTask WriteStickersAsync(
        IReadOnlyList<Sticker> stickers,
        CancellationToken cancellationToken = default
    )
    {
        if (_isNormalized)
        {
            WriteReferenceArray("stickerIds", stickers.Select(RegisterSticker));
            return;
        }

        _writer.WriteStartArray("stickers");

        foreach (var sticker in stickers)
            await WriteStickerAsync(sticker, cancellationToken);

        _writer.WriteEndArray();
    }

    // Resolving the owner as a member makes them appear in the export even when they never posted
    // in any of the exported channels, which is the whole point of recording them. The id is always
    // written; the user object goes inline, or into the lookup table when normalized.
    private async ValueTask WriteOwnerAsync(
        Snowflake? ownerId,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteString("ownerId", ownerId?.ToString());

        if (ownerId is not { } id)
            return;

        await Context.PopulateMemberAsync(id, cancellationToken);

        if (Context.TryGetMember(id)?.User is not { } owner)
            return;

        if (_isNormalized)
        {
            RegisterUser(owner);
        }
        else
        {
            _writer.WritePropertyName("owner");
            await WriteUserAsync(owner, true, cancellationToken);
        }
    }

    // Everything the channel object carries beyond the original DCE schema. Position only exists
    // on ordinary channels, while the member count and the archived/locked state only exist on
    // threads, so in practice a channel has one group or the other, never both.
    private async ValueTask WriteChannelExtrasAsync(
        Channel channel,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteNumber("position", channel.Position);
        _writer.WriteNumber("memberCount", channel.MemberCount);
        _writer.WriteBoolean("isArchived", channel.IsArchived);
        _writer.WriteBoolean("isLocked", channel.IsLocked);

        await WriteOwnerAsync(channel.OwnerId, cancellationToken);
    }

    // Everything the guild object carries beyond the original DCE schema. The role, emoji, and
    // sticker inventories are the guild's full lists, not merely what the exported messages use,
    // so they follow the same inline-vs-reference rule as every other entity in the document.
    private async ValueTask WriteGuildExtrasAsync(
        Guild guild,
        CancellationToken cancellationToken = default
    )
    {
        _writer.WriteString("description", guild.Description);
        _writer.WriteString("vanityUrl", guild.VanityUrl);

        _writer.WriteString(
            "bannerUrl",
            guild.BannerUrl is not null
                ? await Context.ResolveAssetUrlAsync(guild.BannerUrl, cancellationToken)
                : null
        );

        _writer.WriteString(
            "splashUrl",
            guild.SplashUrl is not null
                ? await Context.ResolveAssetUrlAsync(guild.SplashUrl, cancellationToken)
                : null
        );

        _writer.WriteNumber("premiumTier", guild.PremiumTier);
        _writer.WriteNumber("premiumSubscriptionCount", guild.PremiumSubscriptionCount);
        _writer.WriteNumber("approximateMemberCount", guild.ApproximateMemberCount);
        _writer.WriteNumber("approximatePresenceCount", guild.ApproximatePresenceCount);

        await WriteOwnerAsync(guild.OwnerId, cancellationToken);

        // Roles
        if (_isNormalized)
        {
            WriteReferenceArray(
                "roleIds",
                guild.Roles.Select(r =>
                {
                    _roles.TryAdd(r.Id, r);
                    return r.Id.ToString();
                })
            );
        }
        else
        {
            _writer.WritePropertyName("roles");
            await WriteRolesAsync(guild.Roles, cancellationToken);
        }

        // Emoji
        if (_isNormalized)
        {
            WriteReferenceArray("emojiKeys", guild.Emojis.Select(RegisterEmoji));
        }
        else
        {
            _writer.WriteStartArray("emojis");

            foreach (var emoji in guild.Emojis)
                await WriteEmojiAsync(emoji, cancellationToken: cancellationToken);

            _writer.WriteEndArray();
        }

        // Stickers
        await WriteStickersAsync(guild.Stickers, cancellationToken);
    }

    public override async ValueTask WritePreambleAsync(
        CancellationToken cancellationToken = default
    )
    {
        // Root object (start)
        _writer.WriteStartObject();

        // Modifications made by this fork, so that parsers can detect them up-front
        _writer.WriteStartObject("mod");
        _writer.WriteBoolean("normal", _isNormalized);
        _writer.WriteBoolean("extended", _isExtended);
        _writer.WriteBoolean("reactionUsers", _shouldFetchReactionUsers);
        // Provenance: member data in this export may be up to the cache TTL old
        _writer.WriteBoolean("cache", Context.Request.IsCacheEnabled);
        _writer.WriteEndObject();

        // Guild
        var guild = Context.Request.Guild;

        _writer.WriteStartObject("guild");
        _writer.WriteString("id", guild.Id.ToString());
        _writer.WriteString("name", guild.Name);

        _writer.WriteString(
            "iconUrl",
            await Context.ResolveAssetUrlAsync(guild.IconUrl, cancellationToken)
        );

        if (_isExtended)
            await WriteGuildExtrasAsync(guild, cancellationToken);

        _writer.WriteEndObject();

        // Channel
        _writer.WriteStartObject("channel");
        _writer.WriteString("id", Context.Request.Channel.Id.ToString());
        _writer.WriteString("type", Context.Request.Channel.Kind.ToString());

        // Original schema did not account for threads, so 'category' actually refers to the parent channel
        _writer.WriteString("categoryId", Context.Request.Channel.Parent?.Id.ToString());
        _writer.WriteString("category", Context.Request.Channel.Parent?.Name);

        _writer.WriteString("name", Context.Request.Channel.Name);
        _writer.WriteString("topic", Context.Request.Channel.Topic);

        if (!string.IsNullOrWhiteSpace(Context.Request.Channel.IconUrl))
        {
            _writer.WriteString(
                "iconUrl",
                await Context.ResolveAssetUrlAsync(
                    Context.Request.Channel.IconUrl,
                    cancellationToken
                )
            );
        }

        if (_isExtended)
            await WriteChannelExtrasAsync(Context.Request.Channel, cancellationToken);

        _writer.WriteEndObject();

        // Date range
        _writer.WriteStartObject("dateRange");
        _writer.WriteString("after", Context.Request.After?.ToDate().Pipe(Context.NormalizeDate));
        _writer.WriteString("before", Context.Request.Before?.ToDate().Pipe(Context.NormalizeDate));
        _writer.WriteEndObject();

        // Timestamp
        _writer.WriteString("exportedAt", Context.NormalizeDate(DateTimeOffset.UtcNow));

        // Message array (start)
        _writer.WriteStartArray("messages");
        await _writer.FlushAsync(cancellationToken);
    }

    public override async ValueTask WriteMessageAsync(
        Message message,
        CancellationToken cancellationToken = default
    )
    {
        await base.WriteMessageAsync(message, cancellationToken);

        _writer.WriteStartObject();

        // Metadata
        _writer.WriteString("id", message.Id.ToString());
        _writer.WriteString("type", message.Kind.ToString());
        _writer.WriteString("timestamp", Context.NormalizeDate(message.Timestamp));
        _writer.WriteString(
            "timestampEdited",
            message.EditedTimestamp?.Pipe(Context.NormalizeDate)
        );
        _writer.WriteString(
            "callEndedTimestamp",
            message.CallEndedTimestamp?.Pipe(Context.NormalizeDate)
        );
        _writer.WriteBoolean("isPinned", message.IsPinned);

        if (_isExtended)
            WriteReferenceArray("flags", GetFlagNames(message.Flags));

        // Content
        if (message.IsSystemNotification)
        {
            _writer.WriteString("content", message.GetFallbackContent());
        }
        else
        {
            _writer.WriteString(
                "content",
                await FormatMarkdownAsync(message.Content, cancellationToken)
            );
        }

        // Author
        if (_isNormalized)
        {
            _writer.WriteString("authorId", RegisterUser(message.Author));
        }
        else
        {
            _writer.WritePropertyName("author");
            await WriteUserAsync(message.Author, true, cancellationToken);
        }

        // Attachments
        // Not normalized: an attachment belongs to exactly one message, so it never repeats
        _writer.WriteStartArray("attachments");

        foreach (var attachment in message.Attachments)
            await WriteAttachmentAsync(attachment, cancellationToken);

        _writer.WriteEndArray();

        // Embeds
        _writer.WriteStartArray("embeds");

        foreach (var embed in message.Embeds)
            await WriteEmbedAsync(embed, cancellationToken);

        _writer.WriteEndArray();

        // Stickers
        await WriteStickersAsync(message.Stickers, cancellationToken);

        // Components
        if (_isExtended)
            await WriteComponentsAsync(message.Components, cancellationToken);

        // Reactions
        _writer.WriteStartArray("reactions");

        foreach (var reaction in message.Reactions)
        {
            _writer.WriteStartObject();

            // Emoji
            if (_isNormalized)
            {
                _writer.WriteString("emojiKey", RegisterEmoji(reaction.Emoji));
            }
            else
            {
                _writer.WritePropertyName("emoji");
                await WriteEmojiAsync(reaction.Emoji, cancellationToken: cancellationToken);
            }

            _writer.WriteNumber("count", reaction.Count);

            // Reaction authors
            if (_isNormalized)
                _writer.WriteStartArray("userIds");
            else
                _writer.WriteStartArray("users");

            // Fetching the reacting users costs a request per 100 of them, per reaction, which
            // is the single most expensive thing this exporter does. The emoji and the count come
            // free with the message itself, so they are written either way; only the list is
            // skipped. It stays an empty array rather than being omitted, so that the schema is
            // the same in both cases, with 'mod.reactionUsers' recording which one this is.
            if (_shouldFetchReactionUsers)
            {
                await foreach (
                    var user in Context.Discord.GetMessageReactionsAsync(
                        Context.Request.Channel.Id,
                        message.Id,
                        reaction.Emoji,
                        cancellationToken
                    )
                )
                {
                    if (_isNormalized)
                        _writer.WriteStringValue(RegisterUser(user));
                    else
                        await WriteUserAsync(user, false, cancellationToken);
                }
            }

            _writer.WriteEndArray();

            _writer.WriteEndObject();
        }

        _writer.WriteEndArray();

        // Mentions
        if (_isNormalized)
        {
            WriteReferenceArray("mentionIds", message.MentionedUsers.Select(RegisterUser));
        }
        else
        {
            _writer.WriteStartArray("mentions");

            foreach (var user in message.MentionedUsers)
                await WriteUserAsync(user, true, cancellationToken);

            _writer.WriteEndArray();
        }

        // Message reference
        if (message.Reference is not null)
        {
            _writer.WriteStartObject("reference");
            _writer.WriteString("type", message.Reference.Kind.ToString());
            _writer.WriteString("messageId", message.Reference.MessageId?.ToString());
            _writer.WriteString("channelId", message.Reference.ChannelId?.ToString());
            _writer.WriteString("guildId", message.Reference.GuildId?.ToString());
            _writer.WriteEndObject();
        }

        // Forwarded message
        if (message.ForwardedMessage is not null)
        {
            _writer.WriteStartObject("forwardedMessage");

            _writer.WriteString(
                "timestamp",
                Context.NormalizeDate(message.ForwardedMessage.Timestamp)
            );

            _writer.WriteString(
                "timestampEdited",
                message.ForwardedMessage.EditedTimestamp?.Pipe(Context.NormalizeDate)
            );

            _writer.WriteString(
                "content",
                await FormatMarkdownAsync(message.ForwardedMessage.Content, cancellationToken)
            );

            // Forwarded attachments
            _writer.WriteStartArray("attachments");

            foreach (var attachment in message.ForwardedMessage.Attachments)
                await WriteAttachmentAsync(attachment, cancellationToken);

            _writer.WriteEndArray();

            // Forwarded embeds
            _writer.WriteStartArray("embeds");
            foreach (var embed in message.ForwardedMessage.Embeds)
                await WriteEmbedAsync(embed, cancellationToken);
            _writer.WriteEndArray();

            // Forwarded stickers
            await WriteStickersAsync(message.ForwardedMessage.Stickers, cancellationToken);

            // Forwarded components
            if (_isExtended)
                await WriteComponentsAsync(message.ForwardedMessage.Components, cancellationToken);

            _writer.WriteEndObject();
        }

        // Interaction
        if (message.Interaction is not null)
        {
            _writer.WriteStartObject("interaction");

            _writer.WriteString("id", message.Interaction.Id.ToString());
            _writer.WriteString("name", message.Interaction.Name);

            if (_isNormalized)
            {
                _writer.WriteString("userId", RegisterUser(message.Interaction.User));
            }
            else
            {
                _writer.WritePropertyName("user");
                await WriteUserAsync(message.Interaction.User, true, cancellationToken);
            }

            _writer.WriteEndObject();
        }

        // Inline emoji
        var inlineEmojis = MarkdownParser
            .ExtractEmojis(message.Content)
            .DistinctBy(e => e.Name, StringComparer.Ordinal)
            .Select(e => new Emoji(e.Id, e.Name, e.IsAnimated));

        if (_isNormalized)
        {
            WriteReferenceArray("inlineEmojiKeys", inlineEmojis.Select(RegisterEmoji));
        }
        else
        {
            _writer.WriteStartArray("inlineEmojis");

            foreach (var emoji in inlineEmojis)
                await WriteEmojiAsync(emoji, cancellationToken: cancellationToken);

            _writer.WriteEndArray();
        }

        _writer.WriteEndObject();
        await _writer.FlushAsync(cancellationToken);
    }

    // Lookup tables for normalized mode. Entries are sorted so that the output is stable across
    // runs, rather than dependent on the order in which entities happened to be encountered.
    private async ValueTask WriteLookupTablesAsync(CancellationToken cancellationToken = default)
    {
        // Users. Writing these also populates the role table, because a user's roles are
        // resolved from the context here, so this has to come before the roles are written.
        _writer.WriteStartArray("users");

        foreach (var user in _users.Values.OrderBy(u => u.Id.Value))
            await WriteUserAsync(user, true, cancellationToken);

        _writer.WriteEndArray();

        // Roles
        _writer.WritePropertyName("roles");
        await WriteRolesAsync(
            _roles.Values.OrderByDescending(r => r.Position).ThenBy(r => r.Id.Value).ToArray(),
            cancellationToken
        );

        // Emojis
        _writer.WriteStartArray("emojis");

        foreach (var (key, emoji) in _emojis.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
            await WriteEmojiAsync(emoji, key, cancellationToken);

        _writer.WriteEndArray();

        // Stickers
        _writer.WriteStartArray("stickers");

        foreach (var sticker in _stickers.Values.OrderBy(s => s.Id.Value))
            await WriteStickerAsync(sticker, cancellationToken);

        _writer.WriteEndArray();
    }

    public override async ValueTask WritePostambleAsync(
        CancellationToken cancellationToken = default
    )
    {
        // Message array (end)
        _writer.WriteEndArray();

        if (_isNormalized)
            await WriteLookupTablesAsync(cancellationToken);

        _writer.WriteNumber("messageCount", MessagesWritten);

        // Root object (end)
        _writer.WriteEndObject();
        await _writer.FlushAsync(cancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
        await base.DisposeAsync();
    }
}
