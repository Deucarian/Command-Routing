using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deucarian.CommandRouting.Tests
{
    public sealed class CommandRoutingTests
    {
        [Test]
        public void JsonCodec_DecodesCanonicalAndLegacyNames()
        {
            var codec = new JsonCommandProtocolCodec();

            Assert.That(
                codec.TryDecode(
                    "{\"command\":\"refresh\",\"payload\":{\"id\":7}}",
                    out CommandEnvelope canonical,
                    out CommandResult canonicalFailure),
                Is.True);
            Assert.That(canonicalFailure, Is.Null);
            Assert.That(canonical.CommandName, Is.EqualTo("refresh"));
            Assert.That(canonical.Payload.Value<int>("id"), Is.EqualTo(7));

            Assert.That(
                codec.TryDecode(
                    "{\"type\":\"legacy_refresh\"}",
                    out CommandEnvelope legacy,
                    out _),
                Is.True);
            Assert.That(
                legacy.CommandName,
                Is.EqualTo("legacy_refresh"));
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{}")]
        public void JsonCodec_RejectsInvalidEnvelopes(
            string message)
        {
            var codec = new JsonCommandProtocolCodec();

            bool decoded =
                codec.TryDecode(
                    message,
                    out CommandEnvelope command,
                    out CommandResult failure);

            Assert.That(decoded, Is.False);
            Assert.That(command, Is.Null);
            Assert.That(failure.Succeeded, Is.False);
        }

        [Test]
        public void JsonCodec_RedactsSensitiveResultProperties()
        {
            var codec = new JsonCommandProtocolCodec();
            var command =
                new CommandEnvelope(
                    "authenticate",
                    commandId: "request-1");
            var payload = new JObject
            {
                ["access_token"] = "never-return-this",
                ["nested"] = new JObject
                {
                    ["password"] = "also-secret",
                    ["safe"] = "visible"
                }
            };

            string response =
                codec.EncodeResult(
                    command,
                    CommandResult.Success(payload));

            Assert.That(response, Does.Not.Contain("never-return-this"));
            Assert.That(response, Does.Not.Contain("also-secret"));
            Assert.That(response, Does.Contain("visible"));
            Assert.That(response, Does.Contain("request-1"));
        }

        [Test]
        public void Registry_NormalizesAliasesAndRejectsDuplicates()
        {
            var first =
                new StubHandler(
                    new[] { "Refresh Report", "refresh_report" });
            var registry =
                new CommandHandlerRegistry<object>(
                    new[] { first });

            Assert.That(
                registry.TryResolve(
                    " REFRESH_REPORT ",
                    out ICommandHandler<object> resolved,
                    out string normalized),
                Is.True);
            Assert.That(resolved, Is.SameAs(first));
            Assert.That(normalized, Is.EqualTo("refresh_report"));

            Assert.Throws<InvalidOperationException>(
                () => new CommandHandlerRegistry<object>(
                    new[]
                    {
                        first,
                        new StubHandler(
                            new[] { "REFRESH_REPORT" })
                    }));
        }

        [Test]
        public async Task Runtime_RoutesThroughMiddlewareInOrder()
        {
            var order = new List<string>();
            var handler =
                new RecordingApplicationHandler(order);
            var middleware =
                new[]
                {
                    new RecordingMiddleware("outer", order),
                    new RecordingMiddleware("inner", order)
                };
            var application = new List<int>();

            using (var runtime =
                   new CommandRoutingRuntime<List<int>>(
                       application,
                       new[] { handler },
                       new CommandRoutingOptions(),
                       middleware))
            {
                CommandResult result =
                    await runtime.RouteJsonAsync(
                        "{\"command\":\"run\",\"payload\":{\"value\":42}}");

                Assert.That(result.Succeeded, Is.True);
                Assert.That(application, Is.EqualTo(new[] { 42 }));
                Assert.That(
                    order,
                    Is.EqualTo(
                        new[]
                        {
                            "outer:before",
                            "inner:before",
                            "handler",
                            "inner:after",
                            "outer:after"
                        }));
                Assert.That(runtime.History.SucceededCount, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task Runtime_SanitizesHandlerExceptions()
        {
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Command handler failed with " +
                    "InvalidOperationException.*omitted"));
            var handler =
                new StubHandler(
                    new[] { "explode" },
                    (context, token) =>
                        throw new InvalidOperationException(
                            "secret-token-value"));

            using (var runtime =
                   new CommandRoutingRuntime<object>(
                       new object(),
                       new[] { handler }))
            {
                CommandRouteOutcome outcome =
                    await runtime.RouteMessageAsync(
                        "{\"command\":\"explode\"}");

                Assert.That(outcome.Result.Succeeded, Is.False);
                Assert.That(
                    outcome.Result.ErrorCode,
                    Is.EqualTo(
                        CommandRoutingErrorCodes.HandlerFailed));
                Assert.That(
                    outcome.Response,
                    Does.Not.Contain("secret-token-value"));
            }
        }

        [Test]
        public async Task Runtime_RejectsOversizedMessagesBeforeDecode()
        {
            using (var runtime =
                   new CommandRoutingRuntime<object>(
                       new object(),
                       new[] { new StubHandler(new[] { "run" }) },
                       new CommandRoutingOptions(
                           maximumMessageCharacters: 256)))
            {
                CommandResult result =
                    await runtime.RouteJsonAsync(
                        new string('x', 257));

                Assert.That(result.Succeeded, Is.False);
                Assert.That(
                    result.ErrorCode,
                    Is.EqualTo(
                        CommandRoutingErrorCodes.MessageTooLarge));
            }
        }

        [Test]
        public void History_IsBoundedAndNeverStoresPayloads()
        {
            var history = new CommandHistory(2);
            history.Record(
                new CommandEnvelope(
                    "one",
                    new JObject
                    {
                        ["access_token"] = "secret-one"
                    }),
                CommandResult.Success(),
                1d);
            history.Record(
                new CommandEnvelope("two"),
                CommandResult.Failure("failed", "failed"),
                2d);
            history.Record(
                new CommandEnvelope("three"),
                CommandResult.Success(),
                3d);

            IReadOnlyList<CommandHistoryEntry> snapshot =
                history.Snapshot();

            Assert.That(snapshot.Count, Is.EqualTo(2));
            Assert.That(snapshot[0].CommandName, Is.EqualTo("two"));
            Assert.That(snapshot[1].CommandName, Is.EqualTo("three"));
            Assert.That(
                string.Join("|", snapshot),
                Does.Not.Contain("secret-one"));
        }

        [Test]
        public async Task TransportBridge_RoutesAndRepliesThroughTransport()
        {
            var receivedMetadata =
                new TaskCompletionSource<CommandMetadata>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            var handler =
                new StubHandler(
                    new[] { "ping" },
                    (context, token) =>
                    {
                        receivedMetadata.TrySetResult(
                            context.Command.Metadata);
                        return Task.FromResult(
                            CommandResult.Success(
                                new JObject
                                {
                                    ["reply"] = "pong"
                                }));
                    });
            var transport = new FakeTransport("udp");

            using (var runtime =
                   new CommandRoutingRuntime<object>(
                       new object(),
                       new[] { handler }))
            using (var bridge =
                   new CommandTransportBridge<object>(
                       runtime,
                       transport))
            {
                bridge.Start();
                transport.Receive(
                    "{\"command\":\"ping\"}",
                    "127.0.0.1:9000");

                string response =
                    await WithTimeout(transport.Response);
                CommandMetadata metadata =
                    await WithTimeout(receivedMetadata.Task);

                Assert.That(response, Does.Contain("\"pong\""));
                Assert.That(metadata.Transport, Is.EqualTo("udp"));
                Assert.That(
                    metadata.RemoteEndpoint,
                    Is.EqualTo("127.0.0.1:9000"));
                Assert.That(transport.IsRunning, Is.True);
            }

            Assert.That(transport.IsRunning, Is.False);
        }

        private static async Task<T> WithTimeout<T>(
            Task<T> task)
        {
            Task completed =
                await Task.WhenAny(
                    task,
                    Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.That(
                completed,
                Is.SameAs(task),
                "The asynchronous operation timed out.");
            return await task;
        }

        private sealed class StubHandler :
            ICommandHandler<object>
        {
            private readonly Func<
                CommandExecutionContext<object>,
                CancellationToken,
                Task<CommandResult>> callback;

            public StubHandler(
                IReadOnlyList<string> names,
                Func<
                    CommandExecutionContext<object>,
                    CancellationToken,
                    Task<CommandResult>> handler = null)
            {
                CommandNames = names;
                callback =
                    handler ??
                    ((context, token) =>
                        Task.FromResult(
                            CommandResult.Success()));
            }

            public IReadOnlyList<string> CommandNames { get; }

            public Task<CommandResult> HandleAsync(
                CommandExecutionContext<object> context,
                CancellationToken cancellationToken)
            {
                return callback(context, cancellationToken);
            }

        }

        private sealed class RecordingApplicationHandler :
            ICommandHandler<List<int>>
        {
            private readonly List<string> order;

            public RecordingApplicationHandler(
                List<string> calls)
            {
                order = calls;
            }

            public IReadOnlyList<string> CommandNames { get; } =
                new[] { "run" };

            public Task<CommandResult> HandleAsync(
                CommandExecutionContext<List<int>> context,
                CancellationToken cancellationToken)
            {
                order.Add("handler");
                context.Application.Add(
                    context.Command.Payload.Value<int>("value"));
                return Task.FromResult(CommandResult.Success());
            }
        }

        private sealed class RecordingMiddleware :
            ICommandMiddleware<List<int>>
        {
            private readonly string name;
            private readonly List<string> order;

            public RecordingMiddleware(
                string middlewareName,
                List<string> calls)
            {
                name = middlewareName;
                order = calls;
            }

            public async Task<CommandResult> InvokeAsync(
                CommandExecutionContext<List<int>> context,
                CommandHandlerDelegate<List<int>> next,
                CancellationToken cancellationToken)
            {
                order.Add(name + ":before");
                CommandResult result =
                    await next(context, cancellationToken);
                order.Add(name + ":after");
                return result;
            }
        }

        private sealed class FakeTransport :
            ICommandTransport
        {
            private readonly TaskCompletionSource<string>
                response =
                    new TaskCompletionSource<string>(
                        TaskCreationOptions
                            .RunContinuationsAsynchronously);

            public FakeTransport(string transportId)
            {
                TransportId = transportId;
            }

            public string TransportId { get; }
            public bool IsRunning { get; private set; }
            public Task<string> Response => response.Task;

            public event EventHandler<
                CommandTransportMessageEventArgs> MessageReceived;

            public void Start()
            {
                IsRunning = true;
            }

            public void Stop()
            {
                IsRunning = false;
            }

            public Task SendAsync(
                string message,
                string remoteEndpoint,
                CancellationToken cancellationToken)
            {
                response.TrySetResult(message);
                return Task.CompletedTask;
            }

            public void Receive(
                string message,
                string remoteEndpoint)
            {
                MessageReceived?.Invoke(
                    this,
                    new CommandTransportMessageEventArgs(
                        message,
                        remoteEndpoint));
            }

            public void Dispose()
            {
                Stop();
            }
        }
    }
}
