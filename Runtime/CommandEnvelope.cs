using System;
using Newtonsoft.Json.Linq;

namespace Deucarian.CommandRouting
{
    public sealed class CommandEnvelope
    {
        public CommandEnvelope(
            string commandName,
            JObject payload = null,
            string commandId = null,
            int protocolVersion = 1,
            CommandMetadata metadata = null,
            JObject rawEnvelope = null)
        {
            CommandName =
                string.IsNullOrWhiteSpace(commandName)
                    ? string.Empty
                    : commandName.Trim();
            Payload =
                payload == null
                    ? new JObject()
                    : (JObject)payload.DeepClone();
            CommandId =
                string.IsNullOrWhiteSpace(commandId)
                    ? string.Empty
                    : commandId.Trim();
            ProtocolVersion =
                protocolVersion < 1 ? 1 : protocolVersion;
            Metadata = metadata ?? new CommandMetadata();
            RawEnvelope =
                rawEnvelope == null
                    ? new JObject()
                    : (JObject)rawEnvelope.DeepClone();
        }

        public int ProtocolVersion { get; }
        public string CommandId { get; }
        public string CommandName { get; }
        public JObject Payload { get; }
        public CommandMetadata Metadata { get; }
        public JObject RawEnvelope { get; }

        public bool TryReadPayload<T>(
            out T value,
            out string error)
        {
            try
            {
                value = Payload.ToObject<T>();
                if (!ReferenceEquals(value, null))
                {
                    error = string.Empty;
                    return true;
                }

                error =
                    "The command payload could not be mapped to " +
                    typeof(T).Name +
                    ".";
                return false;
            }
            catch (Exception)
            {
                value = default;
                error =
                    "The command payload is malformed for " +
                    typeof(T).Name +
                    ".";
                return false;
            }
        }

        public CommandEnvelope WithTransport(
            string transport,
            string remoteEndpoint)
        {
            return new CommandEnvelope(
                CommandName,
                Payload,
                CommandId,
                ProtocolVersion,
                Metadata.WithTransport(
                    transport,
                    remoteEndpoint),
                RawEnvelope);
        }
    }
}
