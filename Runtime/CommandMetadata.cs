using System;
using Newtonsoft.Json;

namespace Deucarian.CommandRouting
{
    public sealed class CommandMetadata
    {
        public CommandMetadata(
            string source = null,
            string transport = null,
            string remoteEndpoint = null)
        {
            Source = Normalize(source);
            Transport = Normalize(transport);
            RemoteEndpoint = Normalize(remoteEndpoint);
        }

        [JsonProperty("source")]
        public string Source { get; }

        [JsonProperty("transport")]
        public string Transport { get; }

        [JsonProperty("remote_endpoint")]
        public string RemoteEndpoint { get; }

        public CommandMetadata WithTransport(
            string transport,
            string remoteEndpoint)
        {
            return new CommandMetadata(
                Source,
                transport,
                remoteEndpoint);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
