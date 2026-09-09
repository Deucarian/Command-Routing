using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Deucarian.CommandRouting.Tests
{
    public sealed class CommandAsyncContextTests
    {
        private const string Message =
            "{\"command\":\"pending\",\"command_id\":\"pending-1\"}";

        [Test]
        public void Dispatch_PendingHandlerCompletesOnCallerContext()
        {
            using (var context = new CommandAsyncTestContext())
            {
                var handler = new PendingHandler();
                using (var runtime = CreateRuntime(handler))
                {
                    SynchronizationContext observed = null;
                    int count = 0;
                    runtime.Dispatcher.CommandCompleted += (sender, args) =>
                    {
                        observed = SynchronizationContext.Current;
                        count++;
                    };
                    Task<CommandResult> operation = runtime.Dispatcher.DispatchAsync(
                        new CommandEnvelope("pending"));
                    Assert.That(operation.IsCompleted, Is.False);
                    Assert.That(count, Is.Zero);
                    CommandAsyncTestContext.WithoutContext(() => handler.Finish("success"));
                    context.PumpUntil(() => operation.IsCompleted);
                    Assert.That(operation.GetAwaiter().GetResult().Succeeded, Is.True);
                    Assert.That(observed, Is.SameAs(context));
                    Assert.That(count, Is.EqualTo(1));
                    Assert.That(runtime.History.Snapshot().Count, Is.EqualTo(1));
                }
            }
        }

        [TestCase("success")]
        [TestCase("failure")]
        [TestCase("cancelled")]
        public void Route_PendingHandlerEncodesAndNotifiesOnCallerContext(string outcome)
        {
            using (var context = new CommandAsyncTestContext())
            {
                var handler = new PendingHandler();
                var codec = new ContextCodec();
                using (var runtime = CreateRuntime(handler, codec))
                {
                    SynchronizationContext observed = null;
                    int count = 0;
                    runtime.RouteCompleted += (sender, args) =>
                    {
                        observed = SynchronizationContext.Current;
                        count++;
                    };
                    Task<CommandRouteOutcome> operation = runtime.RouteMessageAsync(Message);
                    Assert.That(operation.IsCompleted, Is.False);
                    Assert.That(codec.EncodeCount, Is.Zero);
                    CommandAsyncTestContext.WithoutContext(() => handler.Finish(outcome));
                    context.PumpUntil(() => operation.IsCompleted);
                    CommandRouteOutcome result = operation.GetAwaiter().GetResult();
                    AssertResult(result.Result, outcome);
                    Assert.That(codec.EncodingContext, Is.SameAs(context));
                    Assert.That(codec.EncodeCount, Is.EqualTo(1));
                    Assert.That(observed, Is.SameAs(context));
                    Assert.That(count, Is.EqualTo(1));
                    Assert.That(result.Response, Does.Contain("pending-1"));
                    Assert.That(runtime.History.Snapshot().Count, Is.EqualTo(1));
                }
            }
        }

        [TestCase("success")]
        [TestCase("failure")]
        [TestCase("cancelled")]
        public void Bridge_PendingHandlerAndSendPreserveIngressContext(string outcome)
        {
            using (var context = new CommandAsyncTestContext())
            {
                var handler = new PendingHandler();
                var transport = new PendingTransport();
                using (var runtime = CreateRuntime(handler))
                using (var bridge = new CommandTransportBridge<object>(runtime, transport))
                {
                    bridge.Start();
                    transport.Receive();
                    Assert.That(handler.WasCalled, Is.True);
                    Assert.That(transport.SendCount, Is.Zero);
                    Assert.That(context.PendingOperations, Is.EqualTo(1));
                    CommandAsyncTestContext.WithoutContext(() => handler.Finish(outcome));
                    context.PumpUntil(() => transport.SendCount > 0);
                    int pendingBeforeSendCompletion = context.PendingOperations;
                    CommandAsyncTestContext.WithoutContext(() => transport.FinishSend());
                    context.PumpUntil(() => context.PendingOperations == 0);
                    Assert.That(transport.SendingContext, Is.SameAs(context));
                    Assert.That(transport.Response, Does.Contain("pending-1"));
                    Assert.That(transport.Endpoint, Is.EqualTo("synthetic-endpoint"));
                    Assert.That(transport.Response, Does.Not.Contain("synthetic-secret"));
                    if (outcome != "success")
                        Assert.That(transport.Response,
                            Does.Contain(outcome == "cancelled"
                                ? CommandRoutingErrorCodes.Cancelled : "synthetic_failure"));
                    Assert.That(pendingBeforeSendCompletion, Is.EqualTo(1),
                        "The fake transport send must still be genuinely pending.");
                    Assert.That(context.CompletedOffContext, Is.Zero);
                    Assert.That(transport.SendCount, Is.EqualTo(1));
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Bridge_StoppedGenerationNeverRepliesAfterPendingHandler(bool restart)
        {
            using (var context = new CommandAsyncTestContext())
            {
                var handler = new PendingHandler();
                var transport = new PendingTransport();
                using (var runtime = CreateRuntime(handler))
                using (var bridge = new CommandTransportBridge<object>(runtime, transport))
                {
                    bridge.Start();
                    transport.Receive();
                    Assert.That(context.PendingOperations, Is.EqualTo(1));
                    bridge.Stop();
                    Assert.That(handler.Token.IsCancellationRequested, Is.True);
                    if (restart) bridge.Start();
                    CommandAsyncTestContext.WithoutContext(() => handler.Finish("success"));
                    context.PumpUntil(() => context.PendingOperations == 0);
                    Assert.That(transport.SendCount, Is.Zero);
                    Assert.That(context.CompletedOffContext, Is.Zero);
                    Assert.That(runtime.History.Snapshot().Count, Is.EqualTo(1));
                }
            }
        }

        [Test]
        public void Route_WithoutContextDoesNotCaptureCustomTaskScheduler()
        {
            var handler = new PendingHandler();
            var scheduler = new ConcurrentExclusiveSchedulerPair();
            try
            {
                using (var runtime = CreateRuntime(handler))
                {
                    TaskScheduler completedOn = null;
                    SynchronizationContext completedContext = null;
                    runtime.RouteCompleted += (sender, args) =>
                    {
                        completedOn = TaskScheduler.Current;
                        completedContext = SynchronizationContext.Current;
                    };
                    Task<CommandRouteOutcome> operation = null;
                    Task start = Task.Factory.StartNew(() =>
                    {
                        Assert.That(SynchronizationContext.Current, Is.Null);
                        Assert.That(TaskScheduler.Current, Is.Not.SameAs(TaskScheduler.Default));
                        operation = runtime.RouteMessageAsync(Message);
                    }, CancellationToken.None, TaskCreationOptions.None,
                        scheduler.ExclusiveScheduler);
                    Assert.That(start.Wait(TimeSpan.FromSeconds(3)), Is.True);
                    Assert.That(operation.IsCompleted, Is.False);
                    CommandAsyncTestContext.WithoutContext(() => handler.Finish("success"));
                    Assert.That(operation.Wait(TimeSpan.FromSeconds(3)), Is.True);
                    Assert.That(operation.Result.Result.Succeeded, Is.True);
                    Assert.That(completedOn, Is.SameAs(TaskScheduler.Default));
                    Assert.That(completedContext, Is.Null);
                }
            }
            finally { scheduler.Complete(); }
        }

        private static CommandRoutingRuntime<object> CreateRuntime(
            PendingHandler handler, ICommandProtocolCodec codec = null) =>
            new CommandRoutingRuntime<object>(new object(), new[] { handler },
                new CommandRoutingOptions(logSuccessfulCommands: false,
                    logFailedCommands: false), protocolCodec: codec);

        private static void AssertResult(CommandResult result, string outcome)
        {
            Assert.That(result.Succeeded, Is.EqualTo(outcome == "success"));
            if (outcome != "success")
                Assert.That(result.ErrorCode, Is.EqualTo(outcome == "cancelled"
                    ? CommandRoutingErrorCodes.Cancelled : "synthetic_failure"));
        }

        private sealed class PendingHandler : ICommandHandler<object>
        {
            private readonly TaskCompletionSource<CommandResult> completion =
                new TaskCompletionSource<CommandResult>();
            public IReadOnlyList<string> CommandNames { get; } = new[] { "pending" };
            public bool WasCalled { get; private set; }
            public CancellationToken Token { get; private set; }
            public Task<CommandResult> HandleAsync(CommandExecutionContext<object> context,
                CancellationToken token)
            {
                WasCalled = true;
                Token = token;
                return completion.Task;
            }
            public void Finish(string outcome)
            {
                if (outcome == "cancelled") completion.SetCanceled();
                else completion.SetResult(outcome == "failure"
                    ? CommandResult.Failure("synthetic_failure", "Synthetic failure.")
                    : CommandResult.Success(new JObject { ["token"] = "synthetic-secret" }));
            }
        }

        private sealed class ContextCodec : ICommandProtocolCodec
        {
            private readonly JsonCommandProtocolCodec codec = new JsonCommandProtocolCodec();
            public SynchronizationContext EncodingContext { get; private set; }
            public int EncodeCount { get; private set; }
            public bool TryDecode(string message, out CommandEnvelope command,
                out CommandResult failure) => codec.TryDecode(message, out command, out failure);
            public string EncodeResult(CommandEnvelope command, CommandResult result)
            {
                EncodingContext = SynchronizationContext.Current;
                EncodeCount++;
                return codec.EncodeResult(command, result);
            }
        }

        private sealed class PendingTransport : ICommandTransport
        {
            private readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            public string TransportId => "synthetic-context-transport";
            public bool IsRunning { get; private set; }
            public int SendCount { get; private set; }
            public SynchronizationContext SendingContext { get; private set; }
            public string Response { get; private set; }
            public string Endpoint { get; private set; }
            public event EventHandler<CommandTransportMessageEventArgs> MessageReceived;
            public void Start() => IsRunning = true;
            public void Stop() => IsRunning = false;
            public void Dispose() => Stop();
            public void Receive() => MessageReceived?.Invoke(this,
                new CommandTransportMessageEventArgs(Message, "synthetic-endpoint"));
            public Task SendAsync(string message, string endpoint, CancellationToken token)
            {
                SendingContext = SynchronizationContext.Current;
                Response = message;
                Endpoint = endpoint;
                SendCount++;
                return completion.Task;
            }
            public void FinishSend() => completion.SetResult(true);
        }
    }
}
