# DiscordChatExporter

This is a fork of [DiscordChatExporter](https://github.com/Tyrrrz/DiscordChatExporter) for personal use. It makes heavy use of Claude Code for development and has the following features:

- **Build simplicity**: A build script has been added so building becomes a one-click endeavor.
- **Normalized output**: The original JSON outputs are fully denormalized, leading to orders of magnitude of duplication. A `--normal` CLI option normalizes away all identifiable entities: users, roles, emojis and stickers.
- **Cache results**: Remove redundant requests per run with in-memory cache, and even cross-run redundancy with an on-disk cache file.
- **Extended exports**: Additional fields are exported with the `--extended` flag.