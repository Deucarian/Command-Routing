using System;
using System.Collections.Generic;
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

            if (string.IsNullOrWhiteSpace(message))
            {
                CommandResult emptyFailure =
                    RecordProtocolFailure(
                    CommandResult.Failure(
                        CommandRoutingErrorCodes.EmptyMessage,
                        "A command message is required."));
                return CreateOutcome(null, emptyFailure);
            }

            if (message.Length >
                options.MaximumMessageCharacters)
            {
                CommandResult sizeFailure =
                    RecordProtocolFailure(
                    CommandResult.Failure(
                        CommandRoutingErrorCodes.MessageTooLarge,
                        "The command message exceeds the configured limit."));
                return CreateOutcome(null, sizeFailure);
            }

            if (!codec.TryDecode(
                    message,
                    out CommandEnvelope command,
                    out CommandResult failure))
            {
                CommandResult protocolFailure =
                    RecordProtocolFailure(failure);
                return CreateOutcome(null, protocolFailure);
            }

            if (!string.IsNullOrWhiteSpace(transport) ||
                !string.IsNullOrWhiteSpace(remoteEndpoint))
            {
                command = command.WithTransport(
                    transport,
                    remoteEndpoint);
            }

            CommandResult result =
                await Dispatcher.DispatchAsync(
                        command,
                        cancellationToken)
                    .ConfigureAwait(false);
            return CreateOutcome(command, result);
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

        private CommandResult RecordProtocolFailure(
            CommandResult failure)
        {
            failure =
                failure ??
                CommandResult.Failure(
                    CommandRoutingErrorCodes.MalformedEnvelope,
                    "The command envelope is invalid.");
            History.Record(null, failure, 0d);
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
