using System;
using System.Collections.Generic;

namespace Deucarian.CommandRouting
{
    public sealed class CommandHistory
    {
        private readonly object syncRoot = new object();
        private readonly Queue<CommandHistoryEntry> entries;
        private readonly int capacity;
        private long sequence;
        private long succeededCount;
        private long failedCount;

        public CommandHistory(int historyCapacity)
        {
            capacity = historyCapacity < 1
                ? 1
                : historyCapacity;
            entries =
                new Queue<CommandHistoryEntry>(capacity);
        }

        public long SucceededCount
        {
            get
            {
                lock (syncRoot)
                {
                    return succeededCount;
                }
            }
        }

        public long FailedCount
        {
            get
            {
                lock (syncRoot)
                {
                    return failedCount;
                }
            }
        }

        public void Record(
            CommandEnvelope command,
            CommandResult result,
            double durationMilliseconds)
        {
            result =
                result ??
                CommandResult.Failure(
                    CommandRoutingErrorCodes.HandlerFailed,
                    "The command returned no result.");

            lock (syncRoot)
            {
                sequence++;
                if (result.Succeeded)
                {
                    succeededCount++;
                }
                else
                {
                    failedCount++;
                }

                while (entries.Count >= capacity)
                {
                    entries.Dequeue();
                }

                entries.Enqueue(
                    new CommandHistoryEntry(
                        sequence,
                        DateTime.UtcNow,
                        command?.CommandId,
                        command?.CommandName,
                        command?.Metadata.Source,
                        command?.Metadata.Transport,
                        result.Succeeded,
                        result.ErrorCode,
                        durationMilliseconds));
            }
        }

        public IReadOnlyList<CommandHistoryEntry> Snapshot()
        {
            lock (syncRoot)
            {
                return entries.ToArray();
            }
        }
    }
}
