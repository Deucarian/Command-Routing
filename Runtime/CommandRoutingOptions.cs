namespace Deucarian.CommandRouting
{
    public sealed class CommandRoutingOptions
    {
        public CommandRoutingOptions(
            int historyCapacity =
                CommandRoutingSettings.DefaultHistoryCapacity,
            int maximumMessageCharacters =
                CommandRoutingSettings.DefaultMaximumMessageCharacters,
            bool logSuccessfulCommands = true,
            bool logFailedCommands = true)
        {
            HistoryCapacity =
                historyCapacity < 1 ? 1 : historyCapacity;
            MaximumMessageCharacters =
                maximumMessageCharacters < 256
                    ? 256
                    : maximumMessageCharacters;
            LogSuccessfulCommands =
                logSuccessfulCommands;
            LogFailedCommands =
                logFailedCommands;
        }

        public int HistoryCapacity { get; }
        public int MaximumMessageCharacters { get; }
        public bool LogSuccessfulCommands { get; }
        public bool LogFailedCommands { get; }

        public static CommandRoutingOptions From(
            CommandRoutingSettings settings)
        {
            return settings == null
                ? new CommandRoutingOptions()
                : new CommandRoutingOptions(
                    settings.HistoryCapacity,
                    settings.MaximumMessageCharacters,
                    settings.LogSuccessfulCommands,
                    settings.LogFailedCommands);
        }
    }
}
