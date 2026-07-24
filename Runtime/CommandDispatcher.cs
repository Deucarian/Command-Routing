using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Logging;

namespace Deucarian.CommandRouting
{
    public sealed class CommandDispatcher<TApplicationContext>
    {
        private static readonly DLog Log =
            DLog.For("CommandRouting");

        private readonly TApplicationContext application;
        private readonly CommandHandlerRegistry<TApplicationContext>
            registry;
        private readonly IReadOnlyList<
            ICommandMiddleware<TApplicationContext>> middleware;
        private readonly CommandHistory history;
        private readonly CommandRoutingOptions options;

        public CommandDispatcher(
            TApplicationContext applicationContext,
            CommandHandlerRegistry<TApplicationContext>
                handlerRegistry,
            CommandHistory commandHistory,
            CommandRoutingOptions routingOptions = null,
            IEnumerable<ICommandMiddleware<TApplicationContext>>
                commandMiddleware = null)
        {
            application = applicationContext;
            registry =
                handlerRegistry ??
                throw new ArgumentNullException(
                    nameof(handlerRegistry));
            history =
                commandHistory ??
                throw new ArgumentNullException(
                    nameof(commandHistory));
            options =
                routingOptions ??
                new CommandRoutingOptions();
            middleware =
                commandMiddleware == null
                    ? Array.Empty<
                        ICommandMiddleware<TApplicationContext>>()
                    : new List<
                        ICommandMiddleware<TApplicationContext>>(
                        commandMiddleware);
        }

        public event EventHandler<CommandDispatchEventArgs>
            CommandCompleted;

        public async Task<CommandResult> DispatchAsync(
            CommandEnvelope command,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            CommandResult result;

            if (command == null ||
                string.IsNullOrWhiteSpace(command.CommandName))
            {
                result = CommandResult.Failure(
                    CommandRoutingErrorCodes.MissingCommand,
                    "A command name is required.");
                return Complete(
                    command,
                    result,
                    stopwatch);
            }

            if (!registry.TryResolve(
                    command.CommandName,
                    out ICommandHandler<TApplicationContext>
                        handler,
                    out string normalizedName))
            {
                result = CommandResult.Failure(
                    CommandRoutingErrorCodes.UnsupportedCommand,
                    "Unsupported command: " +
                    command.CommandName +
                    ".");
                return Complete(
                    command,
                    result,
                    stopwatch);
            }

            var context =
                new CommandExecutionContext<TApplicationContext>(
                    application,
                    command,
                    normalizedName);

            try
            {
                CommandHandlerDelegate<TApplicationContext> pipeline =
                    handler.HandleAsync;
                for (int index = middleware.Count - 1;
                     index >= 0;
                     index--)
                {
                    ICommandMiddleware<TApplicationContext> current =
                        middleware[index];
                    CommandHandlerDelegate<TApplicationContext> next =
                        pipeline;
                    pipeline =
                        (executionContext, token) =>
                            current.InvokeAsync(
                                executionContext,
                                next,
                                token);
                }

                result = await pipeline(
                        context,
                        cancellationToken)
                    .ConfigureAwait(false);
                result =
                    result ??
                    CommandResult.Failure(
                        CommandRoutingErrorCodes.HandlerFailed,
                        "The command handler returned no result.");
            }
            catch (OperationCanceledException)
            {
                result = CommandResult.Failure(
                    CommandRoutingErrorCodes.Cancelled,
                    "The command was cancelled.");
            }
            catch (Exception exception)
            {
                Log.Error(
                    "Command handler failed with " +
                    exception.GetType().Name +
                    ". Command payload and exception text were omitted.");
                result = CommandResult.Failure(
                    CommandRoutingErrorCodes.HandlerFailed,
                    "The command handler failed.");
            }

            return Complete(command, result, stopwatch);
        }

        private CommandResult Complete(
            CommandEnvelope command,
            CommandResult result,
            Stopwatch stopwatch)
        {
            stopwatch.Stop();
            double duration =
                stopwatch.Elapsed.TotalMilliseconds;
            history.Record(command, result, duration);

            if (result.Succeeded &&
                options.LogSuccessfulCommands)
            {
                Log.Info(
                    "Completed command '" +
                    (command?.CommandName ?? string.Empty) +
                    "' in " +
                    duration.ToString("F1") +
                    " ms.");
            }
            else if (!result.Succeeded &&
                     options.LogFailedCommands)
            {
                Log.Warning(
                    "Command '" +
                    (command?.CommandName ?? string.Empty) +
                    "' failed with '" +
                    result.ErrorCode +
                    "'.");
            }

            CommandCompleted?.Invoke(
                this,
                new CommandDispatchEventArgs(
                    command,
                    result,
                    duration));
            return result;
        }
    }
}
