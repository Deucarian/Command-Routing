using System;

namespace Deucarian.CommandRouting
{
    public sealed class CommandTransportMessageEventArgs :
        EventArgs
    {
        public CommandTransportMessageEventArgs(
            string message,
            string remoteEndpoint = null)
        {
            Message = message ?? string.Empty;
            RemoteEndpoint =
                string.IsNullOrWhiteSpace(remoteEndpoint)
                    ? string.Empty
                    : remoteEndpoint.Trim();
        }

        public string Message { get; }
        public string RemoteEndpoint { get; }
    }
}
