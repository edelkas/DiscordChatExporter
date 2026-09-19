# DiscordChatExporter

This is a fork of [DiscordChatExporter](https://github.com/Tyrrrz/DiscordChatExporter) for personal use. It makes heavy use of Claude Code for development and has the following features:

- **Extended exports**: Additional fields are exported with the `--extended` flag for guilds, channels, users and messages, including V2 components.
- **Normalized output**: The original JSON outputs are fully denormalized, leading to piles of duplication. A `--normal` CLI option normalizes away all identifiable entities: users, roles, emojis and stickers.
- **Build simplicity**: A build script has been added so building becomes a one-click endeavor.
- **Cache results**: Remove redundant requests per run with in-memory cache via the new `--cache` option, and even cross-run redundancy with an on-disk cache file.
- **Users vs. members**: New `--split-users` option stops merging Discord's global user object with the server-specific member profile, writing them as the two objects they actually are.
- **Member exports**: New `exportusers` command dumps a server's entire roster, keeping the guild-specific member profile and the global user account as separate objects.
- **Bug fixes**:
  * Thread pagination with date ranges works now.
- **Tools**:
  * Script `compare_exports.py` to compare two exports of the same channel and date range.
- **Other changes**:
  * New option `--skip-empty` to not export files with 0 messages.
  * New values for `--include-threads`: `Archived` and `Only` (skips regular channels).
  * Better progress info: message count, MiB count, API request count, etc.