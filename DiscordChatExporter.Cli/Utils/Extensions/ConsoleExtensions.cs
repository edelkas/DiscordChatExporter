using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CliFx.Infrastructure;
using DiscordChatExporter.Cli.Utils;
using DiscordChatExporter.Core.Exporting;
using Spectre.Console;

namespace DiscordChatExporter.Cli.Utils.Extensions;

internal static class ConsoleExtensions
{
    extension(IConsole console)
    {
        public IAnsiConsole CreateAnsiConsole() =>
            AnsiConsole.Create(
                new AnsiConsoleSettings
                {
                    Ansi = AnsiSupport.Detect,
                    ColorSystem = ColorSystemSupport.Detect,
                    Out = new AnsiConsoleOutput(console.Output),
                }
            );

        public Status CreateStatusTicker() =>
            console.CreateAnsiConsole().Status().AutoRefresh(true);

        // The progress bar is deliberately absent: the percentage already carries that number,
        // and the space is better spent on the export's live counters. Those arrive as an extra
        // column, which a caller with nothing to count (the member export) simply omits.
        public Progress CreateProgressTicker(ProgressColumn? extraColumn = null)
        {
            var columns = new List<ProgressColumn>
            {
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new PercentageColumn(),
            };

            if (extraColumn is not null)
                columns.Add(extraColumn);

            return console
                .CreateAnsiConsole()
                .Progress()
                .AutoClear(false)
                .AutoRefresh(true)
                .HideCompleted(false)
                .Columns(columns.ToArray());
        }
    }

    public static async ValueTask StartTaskAsync(
        this ProgressContext context,
        string description,
        Action<ProgressTask>? initialize,
        Func<ProgressTask, ValueTask> performOperationAsync
    )
    {
        // Description cannot be empty
        // https://github.com/Tyrrrz/DiscordChatExporter/issues/1133
        var actualDescription = !string.IsNullOrWhiteSpace(description) ? description : "...";

        var progressTask = context.AddTask(
            actualDescription,
            new ProgressTaskSettings { MaxValue = 1 }
        );

        initialize?.Invoke(progressTask);

        try
        {
            await performOperationAsync(progressTask);
        }
        finally
        {
            progressTask.Value = progressTask.MaxValue;
            progressTask.StopTask();
        }
    }
}
