using System;

namespace Deucarian.CommandRouting
{
    public sealed class CommandDispatchEventArgs : EventArgs
    {
        public CommandDispatchEventArgs(
            CommandEnvelope command,
            CommandResult result,
            double durationMilliseconds)
        {
            Command = command;
            Result = result;
            DurationMilliseconds = durationMilliseconds;
        }

        public CommandEnvelope Command { get; }
        public CommandResult Result { get; }
        public double DurationMilliseconds { get; }
    }
}
