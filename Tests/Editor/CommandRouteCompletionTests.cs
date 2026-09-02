using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deucarian.CommandRouting.Tests
{
    public sealed class CommandRouteCompletionTests
    {
        private const string ExactEndpoint =
            "  synthetic-route/endpoint  ";

        [Test]
        public async Task RouteCompletedCoversEveryTerminalOutcomeExactlyOnce()
        {
            var completions =
                new List<CommandRouteCompletedEventArgs>();
            using (var runtime = CreateRuntime())
            {
                runtime.RouteCompleted +=
                    (sender, args) => completions.Add(args);

                await RouteAndAssert(
                    runtime,
                    completions,
                    string.Empty,
                    CommandRoutingErrorCodes.EmptyMessage,
                    false);
                await RouteAndAssert(
                    runtime,
                    completions,
                    new string('x', 257),
                    CommandRoutingErrorCodes.MessageTooLarge,
                    false);
                await RouteAndAssert(
                    runtime,
                    completions,
                    "{",
                    CommandRoutingErrorCodes.MalformedEnvelope,
                    false);
                await RouteAndAssert(
                    runtime,
                    completions,
                    "{}",
                    CommandRoutingErrorCodes.MissingCommand,
                    false);
                await RouteAndAssert(
                    runtime,
                    completions,
                    "{\"command\":\"unsupported\"}",
                    CommandRoutingErrorCodes.UnsupportedCommand,
                    true);
                await RouteAndAssert(
                    runtime,
                    completions,
                    "{\"command\":\"success\"}",
                    string.Empty,
                    true);
                await RouteAndAssert(
                    runtime,
                    completions,
                    "{\"command\":\"failure\"}",
                    CommandRoutingErrorCodes.HandlerFailed,
                    true);
                await RouteAndAssert(
                    runtime,
                    completions,
                    "{\"command\":\"cancel\"}",
                    CommandRoutingErrorCodes.Cancelled,
                    true);

                Assert.That(completions, Has.Count.EqualTo(8));
                Assert.That(
                    runtime.History.SucceededCount,
                    Is.EqualTo(1));
                Assert.That(
                    runtime.History.FailedCount,
                    Is.EqualTo(7));
                Assert.That(
                    runtime.History.Snapshot().Count,
                    Is.EqualTo(8));
                IReadOnlyList<CommandHistoryEntry> history =
                    runtime.History.Snapshot();
                Assert.That(history[0].DurationMilliseconds, Is.Zero);
                Assert.That(history[1].DurationMilliseconds, Is.Zero);
                Assert.That(history[2].DurationMilliseconds, Is.Zero);
                Assert.That(history[3].DurationMilliseconds, Is.Zero);
            }
        }

        [Test]
        public async Task RouteJsonAndRouteMessageEachPublishOneCompletion()
        {
            int completionCount = 0;
            using (var runtime = CreateRuntime())
            {
                runtime.RouteCompleted +=
                    (sender, args) => completionCount++;

                CommandResult jsonResult =
                    await runtime.RouteJsonAsync(
                        "{\"command\":\"success\"}");
                Assert.That(jsonResult.Succeeded, Is.True);
                Assert.That(completionCount, Is.EqualTo(1));

                CommandRouteOutcome messageOutcome =
                    await runtime.RouteMessageAsync(
                        "{\"command\":\"unsupported\"}");
                Assert.That(messageOutcome.Result.Succeeded, Is.False);
                Assert.That(completionCount, Is.EqualTo(2));
                Assert.That(
                    runtime.History.Snapshot().Count,
                    Is.EqualTo(2));
            }
        }

        [Test]
        public async Task ThrowingSubscriberCannotChangeRoutingOrOtherSubscribers()
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex(
                    "Command route completion subscriber failed.*omitted"));
            CommandRouteCompletedEventArgs observed = null;
            int observedCount = 0;

            using (var runtime = CreateRuntime())
            {
                runtime.RouteCompleted +=
                    (sender, args) =>
                        throw new InvalidOperationException(
                            "synthetic-secret-text");
                runtime.RouteCompleted +=
                    (sender, args) =>
                    {
                        observed = args;
                        observedCount++;
                    };

                CommandRouteOutcome outcome =
                    await runtime.RouteMessageAsync(
                        "{\"command\":\"success\"}",
                        "browser-post-message",
                        ExactEndpoint);

                Assert.That(outcome.Result.Succeeded, Is.True);
                Assert.That(observedCount, Is.EqualTo(1));
                Assert.That(observed.Outcome, Is.Not.SameAs(outcome));
                Assert.That(
                    observed.Outcome.Response,
                    Is.EqualTo(outcome.Response));
                Assert.That(
                    runtime.History.Snapshot().Count,
                    Is.EqualTo(1));
            }
        }

        [Test]
        public void ThrowingLegacyDispatchSubscriberStillPublishesOneRouteCompletion()
        {
            int completionCount = 0;
            CommandRouteCompletedEventArgs completion = null;
            var subscriberFailure = new InvalidOperationException(
                "legacy-dispatch-subscriber-failure");

            using (var runtime = CreateRuntime())
            {
                runtime.Dispatcher.CommandCompleted +=
                    (sender, args) => throw subscriberFailure;
                runtime.RouteCompleted +=
                    (sender, args) =>
                    {
                        completionCount++;
                        completion = args;
                    };

                InvalidOperationException thrown =
                    Assert.ThrowsAsync<InvalidOperationException>(
                        async () => await runtime.RouteMessageAsync(
                            "{\"command\":\"success\"}",
                            "browser-post-message",
                            ExactEndpoint));

                Assert.That(thrown, Is.SameAs(subscriberFailure));
                Assert.That(completionCount, Is.EqualTo(1));
                Assert.That(completion, Is.Not.Null);
                Assert.That(completion.Outcome.Result.Succeeded, Is.True);
                Assert.That(
                    completion.EffectiveTransport,
                    Is.EqualTo("browser-post-message"));
                Assert.That(
                    completion.RemoteEndpoint,
                    Is.EqualTo(ExactEndpoint.Trim()));
                Assert.That(
                    runtime.History.Snapshot().Count,
                    Is.EqualTo(1));
            }
        }

        [Test]
        public async Task SubscribersReceiveIndependentDefensiveOutcomeSnapshots()
        {
            CommandRouteCompletedEventArgs first = null;
            CommandRouteCompletedEventArgs second = null;
            using (var runtime = CreateRuntime())
            {
                runtime.RouteCompleted +=
                    (sender, args) =>
                    {
                        first = args;
                        args.Outcome.Command.Payload["activity_id"] = 999;
                        args.Outcome.Result.Payload["mutated"] = true;
                    };
                runtime.RouteCompleted +=
                    (sender, args) => second = args;

                CommandRouteOutcome outcome =
                    await runtime.RouteMessageAsync(
                        "{\"command\":\"success\",\"payload\":{" +
                        "\"activity_id\":32}}",
                        "browser-post-message",
                        ExactEndpoint);

                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.Not.Null);
                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(first.Outcome, Is.Not.SameAs(second.Outcome));
                Assert.That(first.Outcome, Is.Not.SameAs(outcome));
                Assert.That(second.Outcome, Is.Not.SameAs(outcome));
                Assert.That(
                    second.Outcome.Command.Payload.Value<int>(
                        "activity_id"),
                    Is.EqualTo(32));
                Assert.That(
                    second.Outcome.Result.Payload["mutated"],
                    Is.Null);
                Assert.That(
                    outcome.Command.Payload.Value<int>("activity_id"),
                    Is.EqualTo(32));
                Assert.That(outcome.Result.Payload["mutated"], Is.Null);
            }
        }

        [Test]
        public async Task ThrowingHandlerStillPublishesOneSanitizedCompletion()
        {
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Command handler failed with " +
                    "InvalidOperationException.*omitted"));
            int completionCount = 0;

            using (var runtime = CreateRuntime())
            {
                runtime.RouteCompleted +=
                    (sender, args) => completionCount++;

                CommandRouteOutcome outcome =
                    await runtime.RouteMessageAsync(
                        "{\"command\":\"explode\"}");

                Assert.That(completionCount, Is.EqualTo(1));
                Assert.That(outcome.Result.Succeeded, Is.False);
                Assert.That(
                    outcome.Result.ErrorCode,
                    Is.EqualTo(CommandRoutingErrorCodes.HandlerFailed));
                Assert.That(
                    outcome.Response,
                    Does.Not.Contain("synthetic-secret-text"));
                Assert.That(
                    runtime.History.Snapshot().Count,
                    Is.EqualTo(1));
            }
        }

        [Test]
        public async Task DispatcherCompletionRetainsDispatchOnlySemantics()
        {
            int dispatchCompletionCount = 0;
            int routeCompletionCount = 0;
            CommandDispatchEventArgs dispatched = null;

            using (var runtime = CreateRuntime())
            {
                runtime.Dispatcher.CommandCompleted +=
                    (sender, args) =>
                    {
                        dispatchCompletionCount++;
                        dispatched = args;
                    };
                runtime.RouteCompleted +=
                    (sender, args) => routeCompletionCount++;

                await runtime.RouteMessageAsync("{}");
                Assert.That(dispatchCompletionCount, Is.Zero);
                Assert.That(routeCompletionCount, Is.EqualTo(1));

                await runtime.RouteMessageAsync(
                    "{\"command\":\"success\"}");
                Assert.That(dispatchCompletionCount, Is.EqualTo(1));
                Assert.That(routeCompletionCount, Is.EqualTo(2));
                Assert.That(
                    dispatched.Command.CommandName,
                    Is.EqualTo("success"));
                Assert.That(dispatched.Result.Succeeded, Is.True);
                Assert.That(
                    dispatched.DurationMilliseconds,
                    Is.GreaterThanOrEqualTo(0d));
                Assert.That(
                    runtime.History.Snapshot().Count,
                    Is.EqualTo(2));
            }
        }

        [Test]
        public async Task CompletionUsesEnvelopeTransportWhenNoOverrideExists()
        {
            CommandRouteCompletedEventArgs completion = null;
            using (var runtime = CreateRuntime())
            {
                runtime.RouteCompleted +=
                    (sender, args) => completion = args;

                await runtime.RouteMessageAsync(
                    "{\"command\":\"success\"," +
                    "\"metadata\":{" +
                    "\"transport\":\"envelope-transport\"," +
                    "\"remote_endpoint\":\"envelope-endpoint\"}}",
                    remoteEndpoint: null);

                Assert.That(
                    completion.EffectiveTransport,
                    Is.EqualTo("envelope-transport"));
                Assert.That(
                    completion.RemoteEndpoint,
                    Is.EqualTo("envelope-endpoint"));
                Assert.That(
                    completion.Outcome.Command.Metadata.RemoteEndpoint,
                    Is.EqualTo("envelope-endpoint"));
            }
        }

        [Test]
        public async Task CompletionUsesNormalizedRouteEndpointOverride()
        {
            CommandRouteCompletedEventArgs completion = null;
            using (var runtime = CreateRuntime())
            {
                runtime.RouteCompleted +=
                    (sender, args) => completion = args;

                await runtime.RouteMessageAsync(
                    "{\"command\":\"success\"," +
                    "\"metadata\":{" +
                    "\"transport\":\"envelope-transport\"," +
                    "\"remote_endpoint\":\"envelope-endpoint\"}}",
                    "  route-transport  ",
                    "  route-endpoint  ");

                Assert.That(
                    completion.EffectiveTransport,
                    Is.EqualTo("route-transport"));
                Assert.That(
                    completion.RemoteEndpoint,
                    Is.EqualTo("route-endpoint"));
                Assert.That(
                    completion.Outcome.Command.Metadata.RemoteEndpoint,
                    Is.EqualTo("route-endpoint"));
            }
        }

        [Test]
        public async Task ProtocolRejectionUsesRouteEndpointWithoutCommand()
        {
            CommandRouteCompletedEventArgs completion = null;
            using (var runtime = CreateRuntime())
            {
                runtime.RouteCompleted +=
                    (sender, args) => completion = args;

                CommandRouteOutcome outcome =
                    await runtime.RouteMessageAsync(
                        "{}",
                        "  browser-post-message  ",
                        ExactEndpoint);

                Assert.That(outcome.Command, Is.Null);
                Assert.That(completion.Outcome, Is.Not.SameAs(outcome));
                Assert.That(
                    completion.Outcome.Response,
                    Is.EqualTo(outcome.Response));
                Assert.That(
                    completion.EffectiveTransport,
                    Is.EqualTo("browser-post-message"));
                Assert.That(
                    completion.RemoteEndpoint,
                    Is.EqualTo(ExactEndpoint));
            }
        }

        [Test]
        public void CompletionArgumentsClampInvalidNegativeDurations()
        {
            CommandRouteOutcome outcome = CreateSuccessfulOutcome();
            var notANumber = new CommandRouteCompletedEventArgs(
                outcome,
                "  browser-post-message  ",
                ExactEndpoint,
                double.NaN);
            var negativeInfinity = new CommandRouteCompletedEventArgs(
                outcome,
                string.Empty,
                null,
                double.NegativeInfinity);
            var negative = new CommandRouteCompletedEventArgs(
                outcome,
                string.Empty,
                null,
                -1d);

            Assert.That(notANumber.DurationMilliseconds, Is.Zero);
            Assert.That(negativeInfinity.DurationMilliseconds, Is.Zero);
            Assert.That(negative.DurationMilliseconds, Is.Zero);
            Assert.That(
                notANumber.EffectiveTransport,
                Is.EqualTo("browser-post-message"));
            Assert.That(
                notANumber.RemoteEndpoint,
                Is.EqualTo(ExactEndpoint));
        }

        [Test]
        public void CompletionArgumentsClampInfiniteDuration()
        {
            var args = new CommandRouteCompletedEventArgs(
                CreateSuccessfulOutcome(),
                null,
                null,
                double.PositiveInfinity);

            Assert.That(
                args.DurationMilliseconds,
                Is.EqualTo(TimeSpan.MaxValue.TotalMilliseconds));
            Assert.That(args.EffectiveTransport, Is.Empty);
            Assert.That(args.RemoteEndpoint, Is.Null);
        }

        private static CommandRoutingRuntime<object> CreateRuntime()
        {
            return new CommandRoutingRuntime<object>(
                new object(),
                new[] { new OutcomeHandler() },
                new CommandRoutingOptions(
                    historyCapacity: 32,
                    maximumMessageCharacters: 256,
                    logSuccessfulCommands: false,
                    logFailedCommands: false));
        }

        private static async Task RouteAndAssert(
            CommandRoutingRuntime<object> runtime,
            List<CommandRouteCompletedEventArgs> completions,
            string message,
            string expectedErrorCode,
            bool expectsCommand)
        {
            int completionCount = completions.Count;
            long historyCount =
                runtime.History.SucceededCount +
                runtime.History.FailedCount;

            CommandRouteOutcome outcome =
                await runtime.RouteMessageAsync(
                    message,
                    "  browser-post-message  ",
                    ExactEndpoint);

            Assert.That(
                completions,
                Has.Count.EqualTo(completionCount + 1));
            CommandRouteCompletedEventArgs completion =
                completions[completionCount];
            Assert.That(completion.Outcome, Is.Not.SameAs(outcome));
            Assert.That(
                completion.Outcome.Response,
                Is.EqualTo(outcome.Response));
            Assert.That(
                outcome.Result.ErrorCode,
                Is.EqualTo(expectedErrorCode));
            Assert.That(
                outcome.Command != null,
                Is.EqualTo(expectsCommand));
            Assert.That(
                completion.EffectiveTransport,
                Is.EqualTo("browser-post-message"));
            Assert.That(
                completion.RemoteEndpoint,
                Is.EqualTo(
                    expectsCommand
                        ? ExactEndpoint.Trim()
                        : ExactEndpoint));
            Assert.That(
                completion.DurationMilliseconds,
                Is.InRange(
                    0d,
                    TimeSpan.MaxValue.TotalMilliseconds));
            Assert.That(
                runtime.History.SucceededCount +
                runtime.History.FailedCount,
                Is.EqualTo(historyCount + 1));
        }

        private static CommandRouteOutcome CreateSuccessfulOutcome()
        {
            return new CommandRouteOutcome(
                new CommandEnvelope("success"),
                CommandResult.Success(),
                "{}");
        }

        private sealed class OutcomeHandler : ICommandHandler<object>
        {
            public IReadOnlyList<string> CommandNames { get; } =
                new[]
                {
                    "success",
                    "failure",
                    "cancel",
                    "explode"
                };

            public Task<CommandResult> HandleAsync(
                CommandExecutionContext<object> context,
                CancellationToken cancellationToken)
            {
                switch (context.Command.CommandName)
                {
                    case "failure":
                        return Task.FromResult(
                            CommandResult.Failure(
                                CommandRoutingErrorCodes.HandlerFailed,
                                "Synthetic handler failure."));
                    case "cancel":
                        return Task.FromCanceled<CommandResult>(
                            new CancellationToken(true));
                    case "explode":
                        throw new InvalidOperationException(
                            "synthetic-secret-text");
                    default:
                        return Task.FromResult(
                            CommandResult.Success());
                }
            }
        }
    }
}
