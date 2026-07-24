namespace Deucarian.CommandRouting.Editor
{
    internal static class CommandRoutingSettingsValidation
    {
        public static string Validate(
            CommandRoutingSettings settings)
        {
            if (settings == null)
            {
                return "Create a CommandRoutingSettings asset.";
            }

            if (settings.HistoryCapacity < 1)
            {
                return "History capacity must be at least one.";
            }

            if (settings.MaximumMessageCharacters < 256)
            {
                return "Maximum message characters must be at least 256.";
            }

            return string.Empty;
        }
    }
}
