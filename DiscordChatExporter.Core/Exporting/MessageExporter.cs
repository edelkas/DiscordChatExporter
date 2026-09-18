using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Discord.Data;

namespace DiscordChatExporter.Core.Exporting;

internal partial class MessageExporter(ExportContext context) : IAsyncDisposable
{
    private int _partitionIndex;
    private MessageWriter? _writer;

    // Only the open partition can report its own size, so the ones already closed are kept here
    private long _bytesInClosedPartitions;

    public long MessagesExported { get; private set; }

    public long BytesExported => _bytesInClosedPartitions + (_writer?.BytesWritten ?? 0);

    private async ValueTask<MessageWriter> InitializeWriterAsync(
        CancellationToken cancellationToken = default
    )
    {
        // Ensure that the partition limit has not been reached
        if (
            _writer is not null
            && context.Request.PartitionLimit.IsReached(
                _writer.MessagesWritten,
                _writer.BytesWritten
            )
        )
        {
            await UninitializeWriterAsync(cancellationToken);
            _partitionIndex++;
        }

        // Writer is still valid, return
        if (_writer is not null)
            return _writer;

        Directory.CreateDirectory(context.Request.OutputDirPath);
        var filePath = GetPartitionFilePath(context.Request.OutputFilePath, _partitionIndex);

        var writer = CreateMessageWriter(filePath, context.Request.Format, context);
        await writer.WritePreambleAsync(cancellationToken);

        return _writer = writer;
    }

    private async ValueTask UninitializeWriterAsync(CancellationToken cancellationToken = default)
    {
        if (_writer is not null)
        {
            try
            {
                await _writer.WritePostambleAsync(cancellationToken);
            }
            // Writer must be disposed, even if it fails to write the postamble
            finally
            {
                _bytesInClosedPartitions += _writer.BytesWritten;
                await _writer.DisposeAsync();
                _writer = null;
            }
        }
    }

    public async ValueTask ExportMessageAsync(
        Message message,
        CancellationToken cancellationToken = default
    )
    {
        var writer = await InitializeWriterAsync(cancellationToken);
        await writer.WriteMessageAsync(message, cancellationToken);
        MessagesExported++;

        ExportStats.Current?.ReportExported(MessagesExported, BytesExported);
    }

    public async ValueTask DisposeAsync()
    {
        // If no messages were written, force the creation of an empty file, so that the export
        // still accounts for the channel. The only way nothing is left behind is when the export
        // was explicitly asked to skip channels that turned out to have nothing in range.
        if (MessagesExported <= 0 && !context.Request.ShouldSkipEmptyChannels)
            _ = await InitializeWriterAsync();

        await UninitializeWriterAsync();

        // Report once more now that the postamble is on disk too. Under --normal that's where the
        // lookup tables live, which is a sizeable part of the file, so without this the last
        // number the user sees would sit noticeably below the file they end up with.
        ExportStats.Current?.ReportExported(MessagesExported, BytesExported);
    }
}

internal partial class MessageExporter
{
    private static string GetPartitionFilePath(string baseFilePath, int partitionIndex)
    {
        // First partition, don't change the file name
        if (partitionIndex <= 0)
            return baseFilePath;

        // Inject partition index into the file name
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseFilePath);
        var fileExt = Path.GetExtension(baseFilePath);
        var fileName = $"{fileNameWithoutExt} [part {partitionIndex + 1}]{fileExt}";
        var dirPath = Path.GetDirectoryName(baseFilePath);

        return !string.IsNullOrWhiteSpace(dirPath) ? Path.Combine(dirPath, fileName) : fileName;
    }

    private static MessageWriter CreateMessageWriter(
        string filePath,
        ExportFormat format,
        ExportContext context
    ) =>
        format switch
        {
            ExportFormat.PlainText => new PlainTextMessageWriter(File.Create(filePath), context),
            ExportFormat.Csv => new CsvMessageWriter(File.Create(filePath), context),
            ExportFormat.HtmlDark => new HtmlMessageWriter(File.Create(filePath), context, "Dark"),
            ExportFormat.HtmlLight => new HtmlMessageWriter(
                File.Create(filePath),
                context,
                "Light"
            ),
            ExportFormat.Json => new JsonMessageWriter(File.Create(filePath), context),
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                $"Unknown export format '{format}'."
            ),
        };
}
