using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Deucarian.CommandRouting
{
    public sealed class JsonCommandProtocolCodec :
        ICommandProtocolCodec
    {
        private readonly ICommandRedactor redactor;

        public JsonCommandProtocolCodec(
            ICommandRedactor commandRedactor = null)
        {
            redactor =
                commandRedactor ??
                new DefaultCommandRedactor();
        }

        public bool TryDecode(
            string message,
            out CommandEnvelope command,
            out CommandResult failure)
        {
            command = null;
            if (string.IsNullOrWhiteSpace(message))
            {
                failure = CommandResult.Failure(
                    CommandRoutingErrorCodes.EmptyMessage,
                    "A command message is required.");
                return false;
            }

            try
            {
                JObject root = ParseRootPreservingStringTokens(message);
                string commandName =
                    ReadString(root, "command") ??
                    ReadString(root, "type") ??
                    ReadString(root, "command_name");
                if (string.IsNullOrWhiteSpace(commandName))
                {
                    failure = CommandResult.Failure(
                        CommandRoutingErrorCodes.MissingCommand,
                        "A command name is required.");
                    return false;
                }

                int protocolVersion =
                    root.Value<int?>("protocol_version") ??
                    1;
                string commandId =
                    ReadString(root, "command_id");
                JObject payload =
                    ConvertPayload(root["payload"]);
                CommandMetadata metadata =
                    ReadMetadata(root["metadata"] as JObject);

                command = new CommandEnvelope(
                    commandName,
                    payload,
                    commandId,
                    protocolVersion,
                    metadata,
                    root);
                failure = null;
                return true;
            }
            catch (JsonException)
            {
                failure = CommandResult.Failure(
                    CommandRoutingErrorCodes.MalformedEnvelope,
                    "The command JSON is malformed.");
                return false;
            }
            catch (Exception)
            {
                failure = CommandResult.Failure(
                    CommandRoutingErrorCodes.MalformedEnvelope,
                    "The command envelope is invalid.");
                return false;
            }
        }

        public string EncodeResult(
            CommandEnvelope command,
            CommandResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var root = new JObject
            {
                ["protocol_version"] =
                    command?.ProtocolVersion ?? 1,
                ["command_id"] =
                    command?.CommandId ?? string.Empty,
                ["command"] =
                    command?.CommandName ?? string.Empty,
                ["success"] = result.Succeeded
            };

            if (result.Succeeded)
            {
                root["payload"] =
                    redactor.Redact(result.Payload);
            }
            else
            {
                root["error"] = new JObject
                {
                    ["code"] = result.ErrorCode,
                    ["message"] = result.Message,
                    ["details"] =
                        redactor.Redact(result.Payload)
                };
            }

            return root.ToString(Formatting.None);
        }

        private static JObject ConvertPayload(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return new JObject();
            }

            if (token is JObject payload)
            {
                return payload;
            }

            return new JObject
            {
                ["value"] = token.DeepClone()
            };
        }

        private static JObject ParseRootPreservingStringTokens(
            string message)
        {
            using (var text = new StringReader(message))
            using (var reader = new JsonTextReader(text)
                   {
                       DateParseHandling = DateParseHandling.None
                   })
            {
                JObject root = JObject.Load(reader);
                if (reader.Read())
                {
                    throw new JsonReaderException(
                        "The command JSON contains trailing content.");
                }

                return root;
            }
        }

        private static CommandMetadata ReadMetadata(
            JObject metadata)
        {
            return metadata == null
                ? new CommandMetadata()
                : new CommandMetadata(
                    ReadString(metadata, "source"),
                    ReadString(metadata, "transport"),
                    ReadString(metadata, "remote_endpoint"));
        }

        private static string ReadString(
            JObject source,
            string propertyName)
        {
            JToken token = source[propertyName];
            return token == null ||
                   token.Type == JTokenType.Null
                ? null
                : token.Value<string>();
        }
    }
}
