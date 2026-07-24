using System;

namespace Deucarian.CommandRouting
{
    public sealed class CommandRouteOutcome
    {
        public CommandRouteOutcome(
            CommandEnvelope command,
            CommandResult result,
            string response)
        {
            Command = command;
            Result =
                result ??
                throw new ArgumentNullException(nameof(result));
            Response = response ?? string.Empty;
        }

        public CommandEnvelope Command { get; }
        public CommandResult Result { get; }
        public string Response { get; }
    }
}
