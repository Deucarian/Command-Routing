using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Logging;

namespace Deucarian.CommandRouting
{
    public sealed class CommandTransportBridge<TApplicationContext> :
        IDisposable
    {
        private static readonly DLog Log =
            DLog.For("CommandRouting.Transport");

        private readonly CommandRoutingRuntime<TApplicationContext>
            runtime;
        private readonly ICommandTransport transport;
        private readonly bool sendResponses;
        private readonly bool ownsTransport;
        private CancellationTokenSource cancellation;
        private bool started;
        private bool disposed;

        public CommandTransportBridge(
            CommandRoutingRuntime<TApplicationContext>
                commandRuntime,
            ICommandTransport commandTransport,
            bool shouldSendResponses = true,
            bool disposeTransport = false)
        {
            runtime =
                commandRuntime ??
                throw new ArgumentNullException(
                    nameof(commandRuntime));
            transport =
                commandTransport ??
                throw new ArgumentNullException(
                    nameof(commandTransport));
            sendResponses = shouldSendResponses;
            ownsTransport = disposeTransport;
        }

        public bool IsRunning =>
            started && transport.IsRunning;

        public void Start()
        {
            ThrowIfDisposed();
            if (started)
            {
                return;
            }

            cancellation = new CancellationTokenSource();
            transport.MessageReceived += OnMessageReceived;
            try
            {
                transport.Start();
                started = true;
            }
            catch
            {
                transport.MessageReceived -= OnMessageReceived;
                cancellation.Dispose();
                cancellation = null;
                throw;
            }
        }

        public void Stop()
        {
            if (!started)
            {
                return;
            }

            started = false;
            transport.MessageReceived -= OnMessageReceived;
            cancellation.Cancel();
            transport.Stop();
            cancellation.Dispose();
            cancellation = null;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Stop();
            disposed = true;
            if (ownsTransport)
            {
                transport.Dispose();
            }
        }

        private async void OnMessageReceived(
            object sender,
            CommandTransportMessageEventArgs args)
        {
            CancellationTokenSource source = cancellation;
            if (!started || source == null)
            {
                return;
            }

            try
            {
                CommandRouteOutcome outcome =
                    await runtime.RouteMessageAsync(
                            args.Message,
                            transport.TransportId,
                            args.RemoteEndpoint,
                            source.Token)
                        .ConfigureAwait(false);
                if (sendResponses &&
                    !source.IsCancellationRequested)
                {
                    await transport.SendAsync(
                            outcome.Response,
                            args.RemoteEndpoint,
                            source.Token)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Log.Error(
                    "Command transport bridge failed with " +
                    exception.GetType().Name +
                    ". Message contents and exception text were omitted.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    GetType().Name);
            }
        }
    }
}
