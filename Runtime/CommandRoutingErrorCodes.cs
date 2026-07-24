namespace Deucarian.CommandRouting
{
    public static class CommandRoutingErrorCodes
    {
        public const string EmptyMessage = "empty_command_message";
        public const string MessageTooLarge = "command_message_too_large";
        public const string MalformedEnvelope = "malformed_command_envelope";
        public const string MissingCommand = "missing_command";
        public const string UnsupportedCommand = "unsupported_command";
        public const string HandlerFailed = "command_handler_failed";
        public const string Cancelled = "command_cancelled";
    }
}
