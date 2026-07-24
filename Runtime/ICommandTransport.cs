using System;
using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.CommandRouting
{
    public interface ICommandTransport : IDisposable
    {
        string TransportId { get; }
        bool IsRunning { get; }

        event EventHandler<CommandTransportMessageEventArgs>
            MessageReceived;

        void Start();
        void Stop();

        Task SendAsync(
            string message,
            string remoteEndpoint,
            CancellationToken cancellationToken);
    }
}
