using System.Collections.Generic;
using Deucarian.Diagnostics;

namespace Deucarian.CommandRouting
{
    public sealed class CommandRoutingDiagnosticProvider :
        IDiagnosticProvider
    {
        private readonly string providerId;
        private readonly int handlerCount;
        private readonly int commandNameCount;
        private readonly CommandHistory history;

        public CommandRoutingDiagnosticProvider(
            string runtimeId,
            int registeredHandlerCount,
            int registeredCommandNameCount,
            CommandHistory commandHistory)
        {
            providerId =
                "command-routing." +
                (string.IsNullOrWhiteSpace(runtimeId)
                    ? "runtime"
                    : runtimeId.Trim());
            handlerCount = registeredHandlerCount;
            commandNameCount = registeredCommandNameCount;
            history = commandHistory;
        }

        public string ProviderId => providerId;
        public string DisplayName => "Command Routing";

        public void Collect(DiagnosticReportBuilder builder)
        {
            IReadOnlyList<CommandHistoryEntry> entries =
                history?.Snapshot() ??
                new CommandHistoryEntry[0];
            long succeeded = history?.SucceededCount ?? 0;
            long failed = history?.FailedCount ?? 0;

            DiagnosticSection section =
                builder.AddSection(
                    providerId,
                    DisplayName);
            section.AddItem(
                "handlers",
                "Registered handlers",
                handlerCount.ToString());
            section.AddItem(
                "command_names",
                "Registered command names",
                commandNameCount.ToString());
            section.AddItem(
                "completed",
                "Completed commands",
                succeeded.ToString(),
                DiagnosticSeverity.Success);
            section.AddItem(
                "failed",
                "Failed commands",
                failed.ToString(),
                failed > 0
                    ? DiagnosticSeverity.Warning
                    : DiagnosticSeverity.Success);
            section.AddItem(
                "history",
                "Retained history entries",
                entries.Count.ToString());

            if (entries.Count == 0)
            {
                return;
            }

            CommandHistoryEntry last =
                entries[entries.Count - 1];
            section.AddItem(
                "last_command",
                "Last command",
                last.CommandName);
            section.AddItem(
                "last_transport",
                "Last transport",
                string.IsNullOrWhiteSpace(last.Transport)
                    ? "in-process"
                    : last.Transport);
            section.AddItem(
                "last_duration_ms",
                "Last duration",
                last.DurationMilliseconds.ToString("F1") +
                " ms");
            section.AddItem(
                "last_result",
                "Last result",
                last.Succeeded
                    ? "Succeeded"
                    : last.ErrorCode,
                last.Succeeded
                    ? DiagnosticSeverity.Success
                    : DiagnosticSeverity.Warning);
        }
    }
}
