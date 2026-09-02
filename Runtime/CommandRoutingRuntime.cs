using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Diagnostics;
using Deucarian.Logging;

namespace Deucarian.CommandRouting
{
    public sealed class CommandRoutingRuntime<TApplicationContext> :
        IDisposable,
        ICommandRoutePort
    {
        private static readonly DLog Log =
            DLog.For("CommandRouting");
        private static long nextRuntimeId;

        private readonly ICommandProtocolCodec codec;
        private readonly CommandRoutingOptions options;
        private readonly DiagnosticProviderRegistration
            diagnosticsRegistration;
        private bool disposed;

        public CommandRoutingRuntime(
            TApplicationContext applicationContext,
            IEnumerable<ICommandHandler<TApplicationContext>>
                handlers,
            CommandRoutingSettings settings = null,
            IEnumerable<ICommandMiddleware<TApplicationContext>>
                middleware = null,
            ICommandProtocolCodec protocolCodec = null,
            ICommandNameNormalizer commandNameNormalizer = null)
            : this(
                applicationContext,
                handlers,
                CommandRoutingOptions.From(settings),
                middleware,
                protocolCodec,
                commandNameNormalizer)
        {
        }

        public CommandRoutingRuntime(
            TApplicationContext applicationContext,
            IEnumerable<ICommandHandler<TApplicationContext>>
                handlers,
            CommandRoutingOptions routingOptions,
            IEnumerable<ICommandMiddleware<TApplicationContext>>
                middleware = null,
            ICommandProtocolCodec protocolCodec = null,
            ICommandNameNormalizer commandNameNormalizer = null)
        {
            options =
                routingOptions ??
                new CommandRoutingOptions();
            codec =
                protocolCodec ??
                new JsonCommandProtocolCodec();
            Registry =
                new CommandHandlerRegistry<TApplicationContext>(
                    handlers,
                    commandNameNormalizer);
            History =
                new CommandHistory(options.HistoryCapacity);
            Dispatcher =
                new CommandDispatcher<TApplicationContext>(
                    applicationContext,
                    Registry,
                    History,
                    options,
                    middleware);

            string runtimeId =
                Interlocked.Increment(ref nextRuntimeId)
                    .ToString();
            var provider =
                new CommandRoutingDiagnosticProvider(
                    runtimeId,
                    Registry.HandlerCount,
                    Registry.CommandNameCount,
                    History);
            diagnosticsRegistration =
                DiagnosticProviderRegistry.Register(provider);
        }

        public CommandHandlerRegistry<TApplicationContext>
            Registry { get; }

        public CommandHistory History { get; }

        public CommandDispatcher<TApplicationContext>
            Dispatcher { get; }

        /// <summary>
        /// Raised once after every route attempt has produced its encoded
        /// outcome, including protocol rejections that never reach dispatch.
        /// Subscriber failures are isolated from routing and from one another.
        /// </summary>
        public event EventHandler<CommandRouteCompletedEventArgs>
            RouteCompleted;

        public async Task<CommandResult> RouteJsonAsync(
            string message,
            string transport = null,
            string remoteEndpoint = null,
            CancellationToken cancellationToken = default)
        {
            CommandRouteOutcome outcome =
                await RouteMessageAsync(
                        message,
                        transport,
                        remoteEndpoint,
                        cancellationToken)
                    .ConfigureAwait(false);
            return outcome.Result;
        }

        public async Task<CommandRouteOutcome> RouteMessageAsync(
            string message,
            string transport = null,
            string remoteEndpoint = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var stopwatch = Stopwatch.StartNew();

            if (string.IsNullOrWhiteSpace(message))
            {
                return CompleteRoute(
                    null,
                    CommandResult.Failure(
                        CommandRoutingErrorCodes.EmptyMessage,
                        "A command message is required."),
                    transport,
                    remoteEndpoint,
                    stopwatch,
                    true);
            }

            if (message.Length >
                options.MaximumMessageCharacters)
            {
                return CompleteRoute(
                    null,
                    CommandResult.Failure(
                        CommandRoutingErrorCodes.MessageTooLarge,
                        "The command message exceeds the configured limit."),
                    transport,
                    remoteEndpoint,
                    stopwatch,
                    true);
            }

            if (!codec.TryDecode(
                    message,
                    out CommandEnvelope command,
                    out CommandResult failure))
            {
                return CompleteRoute(
                    null,
                    failure,
                    transport,
                    remoteEndpoint,
                    stopwatch,
                    true);
            }

            if (!string.IsNullOrWhiteSpace(transport) ||
                !string.IsNullOrWhiteSpace(remoteEndpoint))
            {
                command = command.WithTransport(
                    transport,
                    remoteEndpoint);
            }

            CommandResult observedResult = null;
            CommandResult result;
            try
            {
                result = await Dispatcher.DispatchAsync(
                        command,
                        cancellationToken,
                        eventArgs =>
                            observedResult = eventArgs?.Result)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                if (observedResult != null)
                {
                    try
                    {
                        CompleteRoute(
                            command,
                            observedResult,
                            transport,
                            remoteEndpoint,
                            stopwatch,
                            false);
                    }
                    catch (Exception)
                    {
                        Log.Warning(
                            "Command route completion failed while " +
                            "preserving a dispatcher subscriber failure. " +
                            "Exception text was omitted.");
                    }
                }

                throw;
            }

            return CompleteRoute(
                command,
                result,
                transport,
                remoteEndpoint,
                stopwatch,
                false);
        }

        public string EncodeResult(
            CommandEnvelope command,
            CommandResult result)
        {
            ThrowIfDisposed();
            return codec.EncodeResult(command, result);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            diagnosticsRegistration?.Dispose();
        }

        private CommandRouteOutcome CompleteRoute(
            CommandEnvelope command,
            CommandResult result,
            string requestedTransport,
            string remoteEndpoint,
            Stopwatch stopwatch,
            bool recordProtocolFailure)
        {
            result =
                result ??
                CommandResult.Failure(
                    CommandRoutingErrorCodes.MalformedEnvelope,
                    "The command envelope is invalid.");

            if (recordProtocolFailure)
            {
                result = RecordProtocolFailure(result);
            }

            CommandRouteOutcome outcome =
                CreateOutcome(command, result);
            stopwatch.Stop();

            PublishRouteCompleted(
                new CommandRouteCompletedEventArgs(
                    outcome,
                    ResolveEffectiveTransport(
                        command,
                        requestedTransport),
                    ResolveEffectiveRemoteEndpoint(
                        command,
                        remoteEndpoint),
                    stopwatch.Elapsed.TotalMilliseconds));
            return outcome;
        }

        private CommandResult RecordProtocolFailure(
            CommandResult failure)
        {
            History.Record(
                null,
                failure,
                0d);
            if (options.LogFailedCommands)
            {
                Log.Warning(
                    "Command protocol rejected a message with '" +
                    failure.ErrorCode +
                    "'. Payload contents were omitted.");
            }

            return failure;
        }

        private CommandRouteOutcome CreateOutcome(
            CommandEnvelope command,
            CommandResult result)
        {
            return new CommandRouteOutcome(
                command,
                result,
                codec.EncodeResult(command, result));
        }

        private void PublishRouteCompleted(
            CommandRouteCompletedEventArgs args)
        {
            EventHandler<CommandRouteCompletedEventArgs>
                subscribers = RouteCompleted;
            if (subscribers == null)
            {
                return;
            }

            foreach (Delegate subscriber in
                     subscribers.GetInvocationList())
            {
                try
                {
                    ((EventHandler<CommandRouteCompletedEventArgs>)
                        subscriber)(
                            this,
                            args.CreateSubscriberSnapshot());
                }
                catch (Exception)
                {
                    Log.Warning(
                        "Command route completion subscriber failed. " +
                        "Exception text was omitted.");
                }
            }
        }

        private static string ResolveEffectiveTransport(
            CommandEnvelope command,
            string requestedTransport)
        {
            return command == null
                ? requestedTransport
                : command.Metadata.Transport;
        }

        private static string ResolveEffectiveRemoteEndpoint(
            CommandEnvelope command,
            string requestedRemoteEndpoint)
        {
            return command == null
                ? requestedRemoteEndpoint
                : command.Metadata.RemoteEndpoint;
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
