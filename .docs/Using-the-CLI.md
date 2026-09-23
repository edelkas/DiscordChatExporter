# Using the CLI

## Step 1

After extracting the `.zip` archive, open your preferred terminal.

## Step 2

Change the current directory to DCE's folder with `cd C:\path\to\DiscordChatExporter` (`cd /path/to/DiscordChatExporter` on **MacOS** and **Linux**), then press ENTER to run the command.

**Windows** users can quickly get the folder's path by clicking the address bar while inside the folder.
![Copy path from Explorer](https://i.imgur.com/XncnhC2.gif)

**macOS** users can press Command+Option+C (⌘⌥C) while inside the folder (or selecting it) to copy its path to the clipboard.

You can also drag and drop the folder on **every platform**.
![Drag and drop folder](https://i.imgur.com/sOpZQAb.gif)

## Step 3

Now we're ready to run the commands.

Type the following command in your terminal of choice, then press ENTER to run it. This will list all available subcommands and options.

```console
./DiscordChatExporter.Cli
```

> **Note**:
> On Windows, if you're using the default Command Prompt (`cmd`), omit the leading `./` at the start of the command.

> **Docker** users, please refer to the [Docker usage instructions](Docker.md).

## CLI commands

| Command     | Description                                          |
| ----------- | ---------------------------------------------------- |
| export      | Exports a channel                                    |
| exportdm    | Exports all direct message channels                  |
| exportguild | Exports all channels within the specified server     |
| exportall   | Exports all accessible channels                      |
| exportusers | Exports the member list of the specified server      |
| channels    | Outputs the list of channels in the given server     |
| dm          | Outputs the list of direct message channels          |
| guilds      | Outputs the list of accessible servers               |
| guide       | Explains how to obtain token, server, and channel ID |

To use the commands, you'll need a token. For the instructions on how to get a token, please refer to [this page](Token-and-IDs.md), or run `./DiscordChatExporter.Cli guide`.

To get help with a specific command, run:

```console
./DiscordChatExporter.Cli command --help
```

For example, to figure out how to use the `export` command, run:

```console
./DiscordChatExporter.Cli export --help
```

## Export a specific channel

You can quickly export with DCE's default settings by using just `-t token` and `-c channelid`.

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555
```

#### Changing the format

You can change the export format to `HtmlDark`, `HtmlLight`, `PlainText` `Json` or `Csv` with `-f format`. The default
format is `HtmlDark`.

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 -f Json
```

#### Changing the output filename

You can change the filename by using `-o name.ext`. e.g., for the `HTML` format:

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 -o myserver.html
```

#### Changing the output directory

You can change the export directory by using `-o` and providing a path that ends with a slash or does not have a file
extension.
If any of the folders in the path have a space in its name, escape them with quotes (").

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 -o "C:\Discord Exports"
```

#### Changing the filename and output directory

You can change both the filename and export directory by using `-o directory\name.ext`.
Note that the filename must have an extension, otherwise it will be considered a directory name.
If any of the folders in the path have a space in its name, escape them with quotes (").

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 -o "C:\Discord Exports\myserver.html"
```

#### Generating the filename and output directory dynamically

You can use template tokens to generate the output file path based on the server and channel metadata.

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 -o "C:\Discord Exports\%G\%T\%C.html"
```

Assuming you are exporting a channel named `"my-channel"` in the `"Text channels"` category from a server
called `"My server"`, you will get the following output file
path: `C:\Discord Exports\My server\Text channels\my-channel.html`

Here is the full list of supported template tokens:

- `%g` - server ID
- `%G` - server name
- `%t` - category ID
- `%T` - category name
- `%c` - channel ID
- `%C` - channel name
- `%p` - channel position
- `%P` - category position
- `%a` - the "after" date
- `%b` - the "before" date
- `%d` - the current date
- `%%` - escapes `%`

#### Partitioning

You can use partitioning to split files after a given number of messages or file size.
For example, a channel with 36 messages set to be partitioned every 10 messages will output 4 files.

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 -p 10
```

A 45 MB channel set to be partitioned every 20 MB will output 3 files.

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 -p 20mb
```

#### Downloading assets

If this option is set, the export will include additional files such as user avatars, attached files, images, etc.
Only files that are referenced by the export are downloaded, which means that, for example, user avatars will not be
downloaded when using the plain text (TXT) export format.
A folder containing the assets will be created along with the exported chat. They must be kept together.

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 --media
```

#### Reusing assets

Previously downloaded assets can be reused to skip redundant downloads as long as the chat is always exported to the
same folder. Using this option can speed up future exports. This option requires the `--media` option.

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 --media --reuse-media
```

#### Changing the media directory

By default, the media directory is created alongside the exported chat. You can change this by using `--media-dir` and
providing a path that ends with a slash. All of the exported media will be stored in this directory.

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 --media --media-dir "C:\Discord Media"
```

#### Changing the date format

You can customize how dates are formatted in the exported files by using `--locale` and inserting one of Discord's
locales. The default locale is `en-US`.

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 --locale "de-DE"
```

#### Date ranges

**Messages sent before a date**
Use `--before` to export messages sent before the provided date. e.g., messages sent before September 18th, 2019:

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 --before 2019-09-18
```

**Messages sent after a date**
Use `--after` to export messages sent after the provided date. e.g., messages sent after September 17th, 2019 11:34 PM:

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 --after "2019-09-17 23:34"
```

**Messages sent in a date range**
Use `--before` and `--after` to export messages sent during the provided date range. e.g., messages sent between
September 17th, 2019 11:34 PM and September 18th:

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 --after "2019-09-17 23:34" --before "2019-09-18"
```

You can try different formats like `17-SEP-2019 11:34 PM` or even refine your ranges down to
milliseconds `17-SEP-2019 23:45:30.6170`!
Don't forget to quote (") the date if it has spaces!
More info about .NET date
formats [here](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings).

#### Filtering messages

Use `--filter` to filter what messages are included in the export.

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 --filter "from:Tyrrrz has:image"
```

Documentation on message filter syntax can be found [here](https://github.com/Tyrrrz/DiscordChatExporter/blob/prime/.docs/Message-filters.md).

#### Skipping empty channels

> **Note**:
> This option is specific to this fork and is not available in upstream DiscordChatExporter.

A channel with no messages in the exported range still produces a file, containing the preamble and
an empty message list. On a whole-server export narrowed to a date range that is most of the output,
because every channel that happened to be quiet that year is still written out. Use `--skip-empty`
to leave those out:

```console
./DiscordChatExporter.Cli exportguild -t "mfa.Ifrn" -g 21814 -f Json --after 2025-01-01 --before 2026-01-01 --skip-empty
```

The channel is still reported on the console, with the warning saying that no file will be created
rather than that an empty one will be, so you can still tell the difference between a channel that
was skipped and one that was never considered. Nothing else changes: a channel with even one message
in range is written exactly as before.

#### Caching members between runs

> **Note**:
> This option is specific to this fork and is not available in upstream DiscordChatExporter.

Guild members can't be fetched in bulk, so DCE resolves them one at a time, on demand. Within a
single run they're resolved once each, but if you split an export into several invocations — by date
range, or one channel at a time — every run starts from nothing and resolves the same people again.
Splitting a year into monthly runs took one channel's member lookups from 321 to 815.

Use `--cache` to remember them on disk between runs:

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 --cache
```

The cache file defaults to `cache.json` next to the executable, and can be moved with
`--cache-file` or the `DISCORDCHATEXPORTER_CACHE_PATH` environment variable:

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 --cache --cache-file "D:\dce\cache.json"
```

> **Note**:
> Moving it is worth doing if you keep the executable in a folder you rebuild or replace, since the
> default location lives alongside it. Docker users should point it at a mounted volume, as the
> image's application directory is not a good place to keep state.

**Only member lookups are cached.** The server, its channel list, and its roles amount to a handful
of requests per run and are exactly the data that changes wholesale, so they are always fetched
fresh. Messages and reactions are never cached.

##### Staleness

A member's nickname, colour, roles, and avatar are all mutable, and they all end up in the export.
A cached export therefore records them as they were when they were first fetched, which is the one
real cost of this option. `--cache-ttl` bounds it — an entry older than the TTL is refetched:

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 --cache --cache-ttl 12h
```

The default is `1d`. Values can be given as `7d`, `12h`, `90m`, `45s`, or as a full `1.00:00:00`
timespan. `--cache-ttl 0` forces every member to be refetched while still refreshing the cache,
which is the way to deliberately bring a stale cache up to date.

Note that the TTL governs how long an entry is *trusted*, not how long it is *kept*: entries are
retained for 30 days regardless, so running with a short TTL never discards entries that a later
run with a longer one would have accepted.

Any export produced with this option sets `"cache": true` in the `mod` object, so an archive always
says whether its member data could have come from a cache.

##### Other details

- Entries are recorded per server, and tagged with the token that produced them. A different
  account sees a different set of members, so it never reads another account's entries.
- The cache is advisory: if the file is missing, unreadable, or corrupt, it is treated as empty and
  the export proceeds normally, rewriting it on the way out.
- It is written once at the end of a run, atomically, and merged with whatever is already there, so
  several exports can safely share one cache file. A run that you interrupt still saves what it
  learned.

#### Skipping reaction users

> **Note**:
> This option is specific to this fork and is not available in upstream DiscordChatExporter.

For every reaction on every message, the JSON exporter fetches the list of users who reacted, which
costs one request per 100 users *per reaction*. This is routinely the most expensive part of an
export: on a year of a busy channel it accounted for 2,001 of 2,463 requests, dwarfing the cost of
fetching the messages themselves.

The reaction's emoji and its count arrive for free inside the message payload — only the list of
users costs anything. Use `--reaction-users false` to keep the former and skip the latter:

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 -f Json --reaction-users false
```

The `users` (or `userIds`, when normalized) property is still written, as an empty array, so the
schema is unchanged and consumers don't need to special-case it. A reaction only exists when at
least one person reacted, so `count > 0` with an empty user list unambiguously means the list was
skipped rather than that nobody reacted — and `mod.reactionUsers` records it explicitly.

The option is accepted for every format, but only the JSON exporter ever fetched these users in the
first place; HTML, CSV, and plain text exports are byte-identical either way, since they only ever
rendered the emoji and the count.

Two things to be aware of when the option is disabled:

- In normalized exports, users who *only* ever appear as reaction authors no longer appear in the
  root `users` table, because nothing references them any more.
- Combined with `--media`, it also avoids downloading the avatar of every reacting user.

#### Normalizing the JSON output

> **Note**:
> This option is specific to this fork and is not available in upstream DiscordChatExporter.

The default JSON output is fully denormalized: every message carries a complete copy of its
author, and each of those carries a complete copy of each of the author's roles. The same is true
of the users behind each reaction, of emojis, and of stickers. In a real export the same objects
are repeated thousands of times.

Use `--normal` to normalize the output instead. Entities that have an identity are written once
into lookup tables at the root of the document, and referenced by ID from the messages. Because it
restructures the JSON schema, the option is rejected with an error unless `-f Json` is also set;
the other export formats are unaffected by it.

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 -f Json --normal
```

The restructuring is purely a change of shape; no information is lost. These properties are
replaced by references:

| Denormalized                 | Normalized                        | Refers to           |
| ---------------------------- | --------------------------------- | ------------------- |
| `message.author`             | `message.authorId`                | `users`             |
| `message.mentions`           | `message.mentionIds`              | `users`             |
| `message.channelMentions`    | `message.channelMentionIds`       | `channels`          |
| `message.roleMentions`       | `message.roleMentionIds`          | `roles`             |
| `message.stickers`           | `message.stickerIds`              | `stickers`          |
| `message.inlineEmojis`       | `message.inlineEmojiKeys`         | `emojis`            |
| `message.reactions[].emoji`  | `message.reactions[].emojiKey`    | `emojis`            |
| `message.reactions[].users`  | `message.reactions[].userIds`     | `users`             |
| `message.interaction.user`   | `message.interaction.userId`      | `users`             |
| `embed.inlineEmojis`         | `embed.inlineEmojiKeys`           | `emojis`            |
| `user.roles`                 | `user.roleIds`                    | `roles`             |

and lookup tables are added to the root of the document: `users`, `roles`, `emojis` and `stickers`,
plus `members` under `--split-users` and `channels` when `--extended` found a channel mention to put
in it. Entries are sorted, so the output is stable across runs.

The `channels` table holds only the channels some message *mentions*. The channel the export covers
has its own object at the root and is not repeated there.

Attachments are deliberately left inline. An attachment belongs to exactly one message, so unlike
the entities above it is never actually duplicated, and normalizing it would only add indirection.

Users, roles, and stickers are referenced by their Discord ID. Emojis are referenced by a `key`
instead, because only custom emojis have an ID: for a custom emoji the key is its ID, and for a
standard emoji it is the emoji character itself (e.g., `🙂`). Each entry in the `emojis` table
carries its own `key` property. Treat the key as opaque; in the rare case of a custom emoji that
was renamed during its lifetime, older messages still reference the older name, and the two
variants are given distinct keys so that neither name is lost.

Because the tables are per-document, an export split with `--partition` produces partitions that
each remain independently parseable, with tables covering only the entities that partition uses.

One note for consumers that compare the two shapes: in the denormalized output, a user appearing
only as a reaction author is written without roles and with their global display name. In the
normalized output there is a single entry per user, resolved once at the end of the export, so
such a user may carry their guild nickname, color, and roles. The normalized output is therefore a
superset; it never holds less.

#### Extended fields

> **Note**:
> This option is specific to this fork and is not available in upstream DiscordChatExporter.

Discord exposes a good deal more about a user than the original DiscordChatExporter records. Use
`--extended` to include it:

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 53555 -f Json --extended
```

Every field this fork adds on top of the original schema is gated behind this option, so **a
default export stays compatible with vanilla DiscordChatExporter** and can be read by anything
written against it. The only deviation in a default export is the `mod` object described below,
which exists precisely so a parser can tell the two apart.

The option is accepted for every format, but currently only the JSON exporter has any extended
fields to write; the others are unaffected.

##### Server

The `guild` object at the root of the document gains:

| Property | Type | Meaning |
| --- | --- | --- |
| `description` | string or `null` | The server's description |
| `vanityUrl` | string or `null` | Vanity invite, as a full `https://discord.gg/...` URL |
| `bannerUrl` | string or `null` | Server banner |
| `splashUrl` | string or `null` | Invite splash image |
| `premiumTier` | number or `null` | Boost level, 0 to 3 |
| `premiumSubscriptionCount` | number or `null` | Number of boosts |
| `approximateMemberCount` | number or `null` | Approximate total members |
| `approximatePresenceCount` | number or `null` | Approximate members currently online |
| `ownerId` | string or `null` | ID of the server owner |
| `owner` / — | user object | The owner, denormalized mode only (see below) |
| `roles` / `roleIds` | array | **Every** role in the server |
| `emojis` / `emojiKeys` | array | **Every** custom emoji in the server |
| `stickers` / `stickerIds` | array | **Every** sticker in the server |

The role, emoji, and sticker lists are the server's complete inventory, not just what the exported
messages happen to use, so they can be resolved up front without waiting for something to reference
them. They follow the same inline-versus-reference rule as everything else: full objects in a normal
export, IDs into the root lookup tables under `--normal`. Under `--normal` the lookup tables
therefore describe the whole server rather than only what the messages touched.

The owner is fetched explicitly, so they appear in the export even if they never posted in any of
the exported channels. `ownerId` is written in both modes; the owner's full user object appears
inline as `owner` in a normal export, and in the root `users` table under `--normal`. That costs one
extra request per export.

`approximateMemberCount` and `approximatePresenceCount` come from the `with_counts` parameter, which
this fork always sends when fetching a server. The presence count is a live figure, so two exports
taken minutes apart will legitimately differ.

Note that a direct-message export has no real server behind it, so these properties are `null` or
empty there.

##### Channel

The `channel` object gains:

| Property | Type | Meaning |
| --- | --- | --- |
| `position` | number or `null` | Position in the server's channel list |
| `memberCount` | number or `null` | Number of members in a thread |
| `isArchived` | boolean or `null` | Whether a thread is archived |
| `isLocked` | boolean or `null` | Whether a thread is locked |
| `ownerId` | string or `null` | ID of the channel's owner |
| `owner` / — | user object | The owner, denormalized mode only |

These split along the kind of channel being exported. `position` only exists on ordinary channels;
`memberCount`, `isArchived`, and `isLocked` come from a thread's metadata and only exist on threads.
Whichever group doesn't apply is written as `null`, so a `null` here means "not applicable to this
kind of channel" rather than zero or false — an ordinary channel is not an unarchived thread.

`ownerId` is present on threads, where it is whoever created the thread, and on group DMs. Ordinary
guild channels have no owner and report `null`. As with the server owner, the user is fetched
explicitly and so appears in the export even if they never posted in the exported range, `owner`
inline in a normal export and in the root `users` table under `--normal`.

##### Messages

Each message object gains:

| Property | Type | Meaning |
| --- | --- | --- |
| `flags` | array of strings | Message flags that are set |
| `components` | array of objects | The message's component tree |

`flags` follows the same convention as the member flags described below — names rather than a raw
bitfield, with unrecognised bits kept as their numeric value:

```json
"flags": ["IsComponentsV2"]
```

`components` is the interactive and layout content of a message: buttons, select menus, and the
"components v2" layout nodes (sections, text displays, media galleries, containers, and so on) that
a bot can use instead of ordinary message content. It is an empty array for the vast majority of
messages.

This one is worth knowing about even if you don't care about buttons, because a message sent with
the `IsComponentsV2` flag puts **all** of its text in the component tree and leaves `content` empty.
Such a message looks blank in a vanilla export; `--extended` is what recovers it.

Unlike every other part of the export, the component tree is a **faithful mirror of the API
payload** rather than a projection of it. The tree is recursive, every node type has its own set of
fields, and Discord extends it frequently, so any fixed model would quietly start dropping fields as
the API moves on. Two consequences:

- Property names are converted from the API's `snake_case` to the `camelCase` used everywhere else
  in the document (`proxy_url` becomes `proxyUrl`, `content_scan_metadata` becomes
  `contentScanMetadata`), but nothing else is renamed, reordered, added or removed. Discord's
  [component documentation](https://discord.com/developers/docs/components/reference) describes the
  contents directly.
- The tree is identical with and without `--normal`. Emoji on buttons are *not* lifted into the root
  `emojis` table, and component nodes are not deduplicated, so a parser needs only one code path for
  it regardless of the mode.

The single exception to the mirroring is media. A component that references an image or a file does
so through a `media` or `file` object whose `url` and `proxyUrl` are signed CDN links that expire
within a day, so both are put through the asset pipeline like an attachment: unchanged by default,
rewritten to a local path under `--media`. A link button's `url` is deliberately left alone, since it
points at an arbitrary website rather than at a downloadable asset.

> **Note**:
> Under `--media`, `url` and `proxyUrl` name the same image but are distinct URLs, so it is
> downloaded once for each. That costs a duplicate file per component image, in exchange for an
> archive with no expired links left in it.

Forwarded messages carry their own component tree, so `forwardedMessage` gains a `components` array
on the same terms.

##### Mentioned channels and roles

The original schema records the users a message mentions, in `mentions`, and nothing about the
channels or roles it mentions. `--extended` adds the other two, on the same terms:

| Property | Type | Meaning |
| --- | --- | --- |
| `channelMentions` | array of objects | The channels this message mentions |
| `roleMentions` | array of objects | The roles this message mentions |

A mentioned channel carries its identity and nothing else — a mention is a reference, so the topic
and thread state that the exported channel's own object holds have no business here:

```json
{
  "channelMentions": [
    {
      "id": "218819289266913281",
      "type": "GuildTextChat",
      "categoryId": "449367495544143892",
      "category": "Support",
      "name": "support"
    }
  ],
  "roleMentions": [
    { "id": "198374136001593344", "name": "Moderator", "color": "#E67E22", "position": 51 }
  ]
}
```

Both are read out of the message body, the same way `inlineEmojis` is, because Discord's payload
does not carry them: `mention_channels` is only populated for crossposted messages, and
`mention_roles` omits a role mentioned without being pingable. The body is the one place every
mention is always written down. It is read *raw*, so the two arrays say the same thing whether or
not `--markdown` resolved the body into names.

The `id` is always written, even for a channel or role that no longer exists and could not be
resolved; the other fields are then null. An ID is the half of a mention that cannot be recovered
any other way.

Under `--normal` these become `channelMentionIds` and `roleMentionIds`, referring to the root
`channels` and `roles` tables. Note that an ID which could not be resolved still appears in the
reference array while having no entry in the table: a deleted channel leaves nothing to put there
but the ID, and dropping the reference to keep the tables tidy would lose it. This is the one place
in a normalized document where a reference may not resolve.

Together with [Keeping message content stable](#keeping-message-content-stable) this makes a
message's mentions fully recoverable. With `--markdown false` the body keeps `<#218819289266913281>`
and these arrays say what that channel was called at the time — so a consumer can render the
mention without having to resolve anything itself, and without the body changing when somebody
renames the channel later.

##### Users

With `--extended`, each user object in a JSON export gains the following. Note that the user object
has always been a merge of Discord's *user* and *guild member* objects — that is where `nickname`,
`color`, and `roles` already come from — and these follow the same pattern. See
[Splitting users from members](#splitting-users-from-members) for taking the two apart:

| Property | Type | Meaning |
| --- | --- | --- |
| `displayName` | string | Their account-wide display name, with no nickname mixed in |
| `joinedAt` | timestamp or `null` | When the member joined the server |
| `premiumSince` | timestamp or `null` | When they started boosting the server; `null` if they aren't |
| `isPending` | boolean | Whether they still have to pass the server's membership screening |
| `flags` | array of strings | Guild member flags that are set (see below) |
| `bannerUrl` | string or `null` | Their profile banner |

`displayName` is worth singling out, because `nickname` above has already collapsed nickname ->
display name -> username into one string and there is no way to tell the three apart afterwards.
This is the same field the `exportusers` roster and `--split-users` put on the user object.

All of these except `displayName` and `bannerUrl` are guild-scoped, so they are `null` (or
`false`, or an empty array) for anyone who isn't a member of the server. In practice that means
users who left, and users seen only as reaction authors — those are never resolved as members, so
nothing about their membership is known. The export does not invent values for them.

`flags` is written as an array of names rather than the raw bitfield, so that a reader doesn't need
to know the bit values:

```json
"flags": ["DidRejoin", "CompletedOnboarding"]
```

Discord adds member flags over time. Any bit this build doesn't recognise is preserved as its
numeric value in the same array (e.g. `["DidRejoin", "2048"]`), so no information is lost even
against a newer API than the one this was built against.

`bannerUrl` is resolved exactly the way `avatarUrl` is: a server-specific banner takes precedence
over the user's global one, animated banners get a `.gif` URL and static ones `.png`, and with
`--media` the image is downloaded alongside the other assets. The one difference is that there is no
default banner, so unlike `avatarUrl` this property is `null` when the user simply hasn't set one.

> **Note**:
> Discord only includes a banner on the full user object, not on the abbreviated one attached to
> each message, so `bannerUrl` is populated from the member lookup. A user with no member record
> will have `null` here even if they do have a banner set.

#### Splitting users from members

Everywhere a person appears in a JSON export -- a message author, a mention, a reaction author, a
channel or server owner, the invoker of an interaction -- the object written there is a **merge** of
two different things Discord has: the *user* (a global account) and the *guild member* (that
account's profile inside this one server). `nickname`, `color` and `roles` on a user object have
always come from the member; `--extended` adds `joinedAt`, `premiumSince`, `isPending` and `flags`
to the same pile.

`--split-users` writes them as the two objects they actually are, using the same field names as the
[`exportusers`](#export-the-members-of-a-server) document, so one parser covers both:

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 21814 -f Json --split-users
```

```jsonc
"author": {
  "id": "66155023779758080",
  "name": "m.tthew",
  "discriminator": "0000",
  "displayName": "matthew",      // their global display name, never the nickname
  "isBot": false,
  "avatarUrl": "https://cdn.discordapp.com/avatars/...",   // global avatar
  "bannerUrl": null,             // global banner, with --extended
  "member": {
    "userId": "66155023779758080",
    "nickname": "trackpadtimmy", // null when they never set one
    "displayName": "trackpadtimmy",
    "color": "#D7342A",
    "avatarUrl": null,           // server-specific override only
    "bannerUrl": null,           // server-specific override only, with --extended
    "joinedAt": "...",           // with --extended
    "premiumSince": null,        // with --extended
    "isPending": false,          // with --extended
    "flags": [],                 // with --extended
    "roles": [ /* highest first */ ]
  }
}
```

> **Warning**:
> This breaks compatibility with the original DiscordChatExporter schema outright, which is why it
> is a flag of its own rather than part of `--extended`. `mod.splitUsers` records it.

The two flags are orthogonal: `--split-users` changes the *shape*, `--extended` changes which
*fields* exist. Using both gives exactly the field set that `exportusers` writes.

##### Why the nesting is the other way round

In an `exportusers` document the member is the outer object and the user is nested inside it,
because every entry there *is* a member. In a message export the opposite is true: the person is
first and foremost a user, and membership is contingent. So the user is the outer object and
`member` is a property on it that can be `null`.

That null is the point. It means "this export has no member data for them", which covers:

- they left the server, or were never in it, or the account no longer exists;
- nothing in the export ever caused them to be looked up. Reaction authors are the usual case:
  resolving one costs a member lookup, and the exporter will not spend that on every person who
  ever clicked an emoji. A reaction author who also wrote a message in the same export *does* get
  a member object, because by then it is already known.

Written the other way round, those people would get a member object full of nulls, which reads as
"a member who set no nickname and holds no roles" -- a claim the export is in no position to make.

**Splitting never removes information.** Every field is read from exactly the same place the merged
shape reads it, so the two carry the same data and either can be derived from the other. The one
asymmetry is in the merged shape's favour of brevity: it omits the role list for reaction authors,
where the split shape writes the whole member object. `tools/compare_exports.py` understands both,
so a split export can be diffed against a vanilla one directly.

##### With `--normal`

Normalization splits the root `users` table in two, keyed by the same ID, since a member has no
identity of its own:

```jsonc
{
  "users": [ { "id": "...", "name": "...", /* global fields only */ } ],
  "members": [ { "userId": "...", "nickname": "...", "roleIds": ["..."] } ]
}
```

Anyone the server has no member record for is simply absent from `members`. The references
themselves are unchanged: `authorId`, `mentionIds` and the reaction `userIds` are the same IDs as
before, and now resolve against either table.

#### Reading the progress display

> **Note**:
> The columns described here are specific to this fork. Upstream DiscordChatExporter shows a
> progress bar in their place.

While an export runs, each channel gets a line like this:

```
Misc. / other-games              29%       22,001 msg   22.9 MiB in   13.4 MiB out   407 req
Misc. / other-games / Sudoku…   100%        28/28 msg   35.3 KiB in   61.5 KiB out     3 req
```

| Column | Meaning |
| --- | --- |
| `msg` | Messages written so far, and the channel total when Discord reports one |
| `in` | Bytes actually read from Discord for this channel |
| `out` | Bytes written to the output file |
| `req` | HTTP requests issued for this channel |

`in` counts the response bodies as they are consumed, so it includes retries and, with `--media`,
every downloaded asset. Comparing it against `out` is the easiest way to see what `--normal` buys
you: the example above read 22.9 MiB of API responses and wrote 13.4 MiB, because the repeated
author and role objects collapse into the lookup tables.

Work that several channels share is charged to whichever one triggered it. Member lookups are
cached and awaited by every channel that needs them, but only the channel whose request actually
went out pays for it here, so with `--parallel` the per-channel `req` figures are a fair total but
an arbitrary split. The pre-export calls that belong to no channel — resolving the guild, listing
its channels, discovering threads — are not counted against any line.

The display needs roughly 100 columns to lay out without wrapping. It also only appears in an
interactive terminal: when output is redirected to a file or a pipe, the progress renderer falls
back to printing just the channel name and the percentage.

##### How the percentage is estimated

**It is interpolated over time, not over messages.** Discord does not report how many messages a
channel holds, and for a date-ranged export it could not be used anyway, so the exporter measures
the range it has covered instead of the messages it has written.

Before paginating, it fetches the last message in range — one extra request — and keeps it as the
*omega*. The first message that comes back becomes the *alpha*. The percentage is then

```
(current message timestamp - alpha timestamp) / (omega timestamp - alpha timestamp)
```

Consequences worth knowing:

- A channel that was quiet for six months and then busy for a week sits near the same percentage
  for a long time, then crawls through the final stretch. A long dead gap makes it jump.
- It is monotonic, since messages are processed in timestamp order, and it reaches 100% exactly
  when the omega message is written.
- If every message shares one timestamp, the division is skipped and it reports 100%.

The `msg` column is the honest counter: for a thread it shows real progress against Discord's own
total, and everywhere else it at least shows how much work has actually been done. Note that
Discord's thread count excludes the thread starter message and does not track deletions the way an
export does, so the ratio can finish a message or two either side of the total.

#### Comparing two exports

> **Note**:
> This tool is specific to this fork.

`tools/compare_exports.py` checks two JSON exports of the same channel and period against each
other. It reads both schemas — rehydrating a `--normal` export through its lookup tables first — so
a normalized, extended export can be compared directly against a vanilla one:

```console
python tools/compare_exports.py old/export.json new/export.json
python tools/compare_exports.py --dir old/2025 new/2025
```

With `--dir` it walks both trees and compares every pair of files with the same relative path,
reporting the ones present on only one side. Differences are classified, and the exit code is
non-zero only for the first kind:

| Class | Meaning |
| --- | --- |
| `HARD` | message identity or content differs — a real mismatch |
| `SOFT` | a value that genuinely changes between two runs (nickname, username, colour, roles, avatar, reaction counts, `exportedAt`) |
| `MENTN` | message content differing only inside a mention, which the exporter resolves to a *name* at export time, so a rename rewrites old messages |
| `ADDED` | a key present in only one file — the extended fields and the lookup tables themselves |

Signed CDN query parameters (`ex`, `is`, `hm`) are stripped before comparing, since Discord
regenerates them on every request. Mention drift is detected by mapping every display name either
document knows for an ID back to that ID, so a message that mentions a genuinely *different* user
is still reported as `HARD`. Use `--verbose` to list the soft differences too, `--strict` to fail on
them, and `--quiet` for just the summary.

#### Detecting the fork

The JSON output of this fork always includes a `mod` object at the root of the document, which
records which of its modifications are in effect, so that parsers can adapt without guessing:

```json
{
  "mod": {
    "normal": true,
    "extended": true,
    "splitUsers": false,
    "reactionUsers": false,
    "cache": false,
    "markdown": true
  }
}
```

Each key records one of this fork's modifications: `normal` whether the document is normalized,
`extended` whether fields beyond the original schema were written, `splitUsers` whether the user and
guild-member objects are written separately instead of merged, `reactionUsers` whether the users
behind each reaction were fetched, and `cache` whether member data may have been served from a cache
rather than fetched during this export.

`markdown` is the odd one out, being a vanilla option rather than a modification, and it is here
because it is the thing about a message body a consumer most needs to know. See
[Keeping message content stable](#keeping-message-content-stable).

With `normal`, `extended` and `splitUsers` all false, the document matches the schema of a vanilla
DiscordChatExporter export, apart from the presence of this `mod` object itself.

An export produced by upstream DiscordChatExporter has no `mod` property at all.

#### Keeping message content stable

`--markdown` is on by default, and it does rather more to a message body than the name suggests.
For the JSON format it resolves exactly three things, and leaves everything else alone:

| In the message | Written as |
| --- | --- |
| `<@197765375503368192>` | `@Nickname` |
| `<#449367560878686208>` | `#channel-name`, plus ` [voice]` for a voice channel |
| `<@&198374136001593344>` | `@Role Name` |
| `<:goldheart:711809267547766806>` | `:goldheart:` |
| `<t:1735689600:R>` | a date, formatted per `--locale` |

Every one of those is resolved against something that can change later. Rename a channel, change a
nickname, export the same conversation again, and the message body comes out different although
nobody edited anything. For a one-off export that is merely cosmetic. For an archive kept up to
date by re-exporting, it is worse than cosmetic: a consumer diffing old against new sees hundreds
of messages that appear to have been edited, and cannot tell those from the handful that really
were.

So for anything being archived rather than read, export with markdown off:

```console
./DiscordChatExporter.Cli export -t "mfa.Ifrn" -c 21814 -f Json --markdown false
```

The bodies then keep their raw form, which is both stable forever and strictly more informative --
`<:goldheart:711809267547766806>` names the emoji *and* identifies it, where `:goldheart:` only
names it. The `mod` object records which way the export was made, so a consumer never has to guess.

Note that this only concerns what is written into `content`. The structured fields are unaffected:
`mentions` still lists the users a message mentions, `inlineEmojis` is still extracted, and both are
built from the raw message either way.

### Export channels from a specific server

To export all channels in a specific server, use the `exportguild` command and provide the server ID through the `-g|--guild` option:

```console
./DiscordChatExporter.Cli exportguild -t "mfa.Ifrn" -g 21814
```

#### Including threads

By default, threads are not included in the export. Use `--include-threads` to change that:

```console
./DiscordChatExporter.Cli exportguild -t "mfa.Ifrn" -g 21814 --include-threads all
```

| Value | Channels | Threads |
| --- | --- | --- |
| `none` (default) | yes | none |
| `active` | yes | active only |
| `archived` | yes | archived only |
| `all` | yes | active and archived |
| `only` | **no** | active and archived |

> **Note**:
> `archived` and `only` are specific to this fork and are not available in upstream
> DiscordChatExporter, which offers `none`, `active` and `all`.

`archived` is the complement of `active`: the two are fetched from different endpoints, so asking
for one skips the other's requests entirely rather than fetching both and filtering afterwards.
Together they cover exactly what `all` does.

`only` exports the threads without the channels they hang off. The channels are still resolved,
because that is the only way to discover threads in the first place, but they are dropped before
anything is written. Everything that isn't a thread goes, which includes voice channels and forums,
so `--include-vc` has no effect alongside it. This is what you want for a run that complements an
earlier `--include-threads none` export of the same period, rather than re-exporting every channel:

```console
./DiscordChatExporter.Cli exportguild -t "mfa.Ifrn" -g 21814 -f Json --include-threads only -o "threads/%C.json"
```

Because `only` can still yield many files, the output path has to be a directory or contain
template tokens, exactly as with the other thread modes.

The `channels` command accepts the same values. There, `only` lists the channels that hold threads
together with those threads, and omits the channels that have none — a channel is listed for the
sake of what is underneath it, so there is nothing to show for an empty one.

For backwards compatibility `--include-threads true` still means `active` and
`--include-threads false` means `none`, and the value is matched case-insensitively.

> **Note**:
> Upstream DiscordChatExporter drops archived threads when the export is also narrowed by
> `--before`. Discord's archived-thread listing is paginated by *archive* timestamp, and upstream
> seeds that cursor with the `--before` boundary, which is a *message* timestamp — so a thread that
> was busy inside the range but archived after it ends is never even listed. Exporting 2025 with
> `--before 2026-01-01` therefore silently loses every thread archived in 2026. This fork pages
> through the listing on its own terms and applies the date range to each thread individually,
> using the thread's creation and last-message IDs, which are exact. The symptom is easy to miss
> because it disappears whenever the range runs up to the present.

#### Including voice channels

By default, voice channels are included in the export. You can change this behavior by using `--include-vc` and
specifying whether to include voice channels in the export. It has possible values of `true` or `false`, to exclude
voice channels, use `--include-vc false`.

```console
./DiscordChatExporter.Cli exportguild -t "mfa.Ifrn" -g 21814 --include-vc false
```

### Export all channels

To export all accessible channels, use the `exportall` command:

```console
./DiscordChatExporter.Cli exportall -t "mfa.Ifrn"
```

#### Excluding DMs

To exclude DMs, add the `--include-dm false` option.

```console
./DiscordChatExporter.Cli exportall -t "mfa.Ifrn" --include-dm false
```

### Export the members of a server

Everything else in this tool exports *messages*, and the people in those exports are described only
as far as the messages needed. The `exportusers` command exports the server's roster instead: every
member, whether or not they ever said anything.

```console
./DiscordChatExporter.Cli exportusers -t "mfa.Ifrn" -g 21814
```

> **Note**:
> This command is specific to this fork, and it is JSON-only. It needs a **bot token** whose
> application has the **Server Members Intent** enabled (Discord developer portal, under
> Bot -> Privileged Gateway Intents). Discord does not hand the member list to user accounts over
> the REST API at all. Without the intent the command stops with an explicit message rather than a
> bare "forbidden".

It is cheap: the roster comes 1000 members at a time, so a 5000-member server costs six requests.

#### Members vs. users

The two are not the same thing, and this document is the one place the distinction is drawn
explicitly:

- A **user** is a Discord account. Their username, global display name, avatar and banner look the
  same in every server.
- A **member** is that account's profile *inside one server*: their nickname, their roles, the
  colour those roles give them, a server-specific avatar or banner, when they joined, and whether
  they are boosting.

A member has no identity of its own, which is why the entry is keyed by `userId` rather than by an
ID of its own.

Elsewhere in the export format the two are merged: the `author` of a message is a user object with
the member's `nickname`, `color` and `roles` folded into it. Here they stay apart, so a null
`avatarUrl` on a member means "wears their global avatar" rather than "has no avatar".

#### Output

By default the file is written to the working directory as `<server> [<id>] - Members.json`. The
`-o|--output` option takes a file or a directory, and supports the `%g` (server ID), `%G` (server
name), `%d` (current date) and `%%` tokens:

```console
./DiscordChatExporter.Cli exportusers -t "mfa.Ifrn" -g 21814 -o "C:\rosters\%G %d.json"
```

The document looks like this:

```jsonc
{
  "mod": { "normal": false, "fullUsers": false },
  "guild": { "id": "...", "name": "...", "roles": [ /* the server's whole role inventory */ ] },
  "exportedAt": "...",
  "members": [
    {
      "userId": "66155023779758080",
      "nickname": "trackpadtimmy",      // server-specific; null when never set
      "displayName": "trackpadtimmy",   // what a client renders: nickname, else the user's own
      "color": "#D7342A",               // from the highest-positioned role that has one
      "avatarUrl": null,                // server-specific override only
      "bannerUrl": null,                // server-specific override only
      "joinedAt": "2017-10-19T04:11:02.439+02:00",
      "premiumSince": null,             // set only while boosting
      "isPending": false,               // has not passed membership screening yet
      "flags": ["DidRejoin"],
      "roles": [ /* highest first, @everyone excluded, as Discord sends it */ ],
      "user": {
        "id": "66155023779758080",
        "name": "m.tthew",
        "discriminator": "0000",
        "displayName": "matthew",
        "isBot": false,
        "avatarUrl": "https://cdn.discordapp.com/avatars/...",
        "bannerUrl": null
      }
    }
  ],
  "memberCount": 5215
}
```

`guild.approximateMemberCount` is Discord's own estimate and `memberCount` is what was actually
written. The two disagreeing by a few is normal and does not mean the export was truncated.

Avatar and banner hashes are resolved to CDN URLs the same way as everywhere else: an `a_` prefix
means the asset is animated and gets a `.gif` URL, and the server-specific one takes precedence
over the global one when both exist. Unlike a channel export, these are always real CDN links --
there is no `--media` option here, so nothing is downloaded.

#### Normalizing the output

`--normal` works as it does for a channel export: users and roles move into lookup tables at the
root, and the members reference them by ID.

```console
./DiscordChatExporter.Cli exportusers -t "mfa.Ifrn" -g 21814 --normal
```

Each member has `roleIds` instead of `roles` and drops the nested `user` for the root `users`
table. Every member has exactly one user, so this deduplicates nothing there -- the saving is in
the roles, which would otherwise be repeated in full for every member wearing them.

#### Resolving full user profiles

The user object nested in a roster entry is a **partial** one. Discord sends `banner` as null on it
whether or not the account has one, so without help every `user.bannerUrl` in the document comes
out null. Only a dedicated per-user fetch carries the real value, which is what `--full-users` does:

```console
./DiscordChatExporter.Cli exportusers -t "mfa.Ifrn" -g 21814 --full-users
```

This costs **one extra request per member**, turning a six-request export into a five-thousand-
request one, which is why it is opt-in. Measured against a 5215-member server, it settles at about
one member per second, so budget roughly an hour and a half; `--respect-rate-limits false` trades
politeness for speed. `mod.fullUsers` records which way the document was produced, so a consumer
can tell "this user has no banner" from "nobody asked".

Server-specific member banners are unaffected: those come from the member object and are always
present.

### List channels in a server

To list the channels available in a specific server, use the `channels` command and provide the server ID through the `-g|--guild` option:

```console
./DiscordChatExporter.Cli channels -t "mfa.Ifrn" -g 21814
```

### List direct message channels

To list all DM channels accessible to the current account, use the `dm` command:

```console
./DiscordChatExporter.Cli dm -t "mfa.Ifrn"
```

### List servers

To list all servers accessible by the current account, use the `guilds` command:

```console
./DiscordChatExporter.Cli guilds -t "mfa.Ifrn" > C:\path\to\output.txt
```
