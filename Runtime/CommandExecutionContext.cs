using System;

namespace Deucarian.CommandRouting
{
    public sealed class CommandExecutionContext<TApplicationContext>
    {
        public CommandExecutionContext(
            TApplicationContext application,
            CommandEnvelope command,
            string normalizedCommandName)
        {
            Application = application;
            Command =
                command ??
                throw new ArgumentNullException(nameof(command));
            NormalizedCommandName =
                string.IsNullOrWhiteSpace(normalizedCommandName)
                    ? command.CommandName
                    : normalizedCommandName;
        }

        public TApplicationContext Application { get; }
        public CommandEnvelope Command { get; }
        public string NormalizedCommandName { get; }
    }
}
