using Newtonsoft.Json.Linq;

namespace Deucarian.CommandRouting
{
    public sealed class CommandResult
    {
        private CommandResult(
            bool succeeded,
            string errorCode,
            string message,
            JObject payload)
        {
            Succeeded = succeeded;
            ErrorCode = Normalize(errorCode);
            Message = Normalize(message);
            Payload =
                payload == null
                    ? new JObject()
                    : (JObject)payload.DeepClone();
        }

        public bool Succeeded { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public JObject Payload { get; }

        public static CommandResult Success(
            JObject payload = null,
            string message = null)
        {
            return new CommandResult(
                true,
                null,
                message,
                payload);
        }

        public static CommandResult Failure(
            string errorCode,
            string message,
            JObject payload = null)
        {
            return new CommandResult(
                false,
                string.IsNullOrWhiteSpace(errorCode)
                    ? CommandRoutingErrorCodes.HandlerFailed
                    : errorCode,
                string.IsNullOrWhiteSpace(message)
                    ? "The command failed."
                    : message,
                payload);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
