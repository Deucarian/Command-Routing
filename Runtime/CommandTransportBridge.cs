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

            transport.MessageReceived -= OnMessageReceived;
            cancellation.Cancel();
            try
            {
                transport.Stop();
            }
            catch
            {
                // Keep the bridge retryable while the underlying transport is
                // still active. The handler remains detached and the current
                // dispatch generation remains cancelled until Stop succeeds.
                throw;
            }

            started = false;
            DisposeCancellation();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Exception failure = null;
            try
            {
                Stop();
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            disposed = true;
            started = false;
            transport.MessageReceived -= OnMessageReceived;
            DisposeCancellation();

            if (ownsTransport)
            {
                try
                {
                    transport.Dispose();
                }
                catch (Exception exception)
                {
                    failure = CombineFailures(failure, exception);
                }
            }

            if (failure != null)
            {
                throw failure;
            }
        }

        private void DisposeCancellation()
        {
            cancellation?.Dispose();
            cancellation = null;
        }

        private static Exception CombineFailures(
            Exception first,
            Exception second)
        {
            if (first == null)
            {
                return second;
            }

            return new AggregateException(
                "Command transport bridge cleanup failed.",
                first,
                second);
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
