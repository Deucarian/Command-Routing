using System;

namespace Deucarian.CommandRouting
{
    public sealed class CommandHistoryEntry
    {
        public CommandHistoryEntry(
            long sequence,
            DateTime timestampUtc,
            string commandId,
            string commandName,
            string source,
            string transport,
            bool succeeded,
            string errorCode,
            double durationMilliseconds)
        {
            Sequence = sequence;
            TimestampUtc = timestampUtc;
            CommandId = commandId ?? string.Empty;
            CommandName = commandName ?? string.Empty;
            Source = source ?? string.Empty;
            Transport = transport ?? string.Empty;
            Succeeded = succeeded;
            ErrorCode = errorCode ?? string.Empty;
            DurationMilliseconds = durationMilliseconds;
        }

        public long Sequence { get; }
        public DateTime TimestampUtc { get; }
        public string CommandId { get; }
        public string CommandName { get; }
        public string Source { get; }
        public string Transport { get; }
        public bool Succeeded { get; }
        public string ErrorCode { get; }
        public double DurationMilliseconds { get; }
    }
}
