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
| `message.stickers`           | `message.stickerIds`              | `stickers`          |
| `message.inlineEmojis`       | `message.inlineEmojiKeys`         | `emojis`            |
| `message.reactions[].emoji`  | `message.reactions[].emojiKey`    | `emojis`            |
| `message.reactions[].users`  | `message.reactions[].userIds`     | `users`             |
| `message.interaction.user`   | `message.interaction.userId`      | `users`             |
| `embed.inlineEmojis`         | `embed.inlineEmojiKeys`           | `emojis`            |
| `user.roles`                 | `user.roleIds`                    | `roles`             |

and four lookup tables are added to the root of the document: `users`, `roles`, `emojis`, and
`stickers`. Entries are sorted, so the output is stable across runs.

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

#### Detecting the fork

The JSON output of this fork always includes a `mod` object at the root of the document, which
records which of its modifications are in effect, so that parsers can adapt without guessing:

```json
{
  "mod": {
    "normal": true
  }
}
```

An export produced by upstream DiscordChatExporter has no `mod` property at all.

### Export channels from a specific server

To export all channels in a specific server, use the `exportguild` command and provide the server ID through the `-g|--guild` option:

```console
./DiscordChatExporter.Cli exportguild -t "mfa.Ifrn" -g 21814
```

#### Including threads

By default, threads are not included in the export. You can change this behavior by using `--include-threads` and
specifying which threads should be included. It has possible values of `none`, `active`, or `all`, indicating which
threads should be included. To include both active and archived threads, use `--include-threads all`.

```console
./DiscordChatExporter.Cli exportguild -t "mfa.Ifrn" -g 21814 --include-threads all
```

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
