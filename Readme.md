# DiscordChatExporter

This is a fork of [DiscordChatExporter](https://github.com/Tyrrrz/DiscordChatExporter) for personal use. It makes heavy use of Claude Code for development and has the following features:

- **Build simplicity**: A build script has been added so building becomes a one-click endeavor.
- **Output efficiency**: The original JSON outputs are fully denormalized, leading to orders of magnitude of duplication. Indeed, they can be compressed to 3% of their original size. Now a `--normal` CLI option normalizes away all identifiable entities: users, roles, emojis and stickers.