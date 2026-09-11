using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.CommandRouting.Tests
{
    public sealed class CommandHostTests
    {
        [Test]
        public async Task TypedCallReachesTheRegisteredHandlerAndPreservesFailures()
        {
            var go = new GameObject("commands");
            var context = new Context();
            using (var runtime = new CommandRoutingRuntime<Context>(context, new[] { new Handler() }))
            {
                try
                {
                    var host = go.AddComponent<CommandHost>();
                    host.Configure(runtime, new[] { CommandPayloadBinding.For<string>("set_message", value => new JObject { ["message"] = value }) });
                    Assert.That((await host.ExecuteAsync("set_message", "Hello")).Succeeded, Is.True);
                    Assert.That(context.Message, Is.EqualTo("Hello"));
                    Assert.That((await host.ExecuteAsync("missing", "Hello")).Succeeded, Is.False);
                    Assert.ThrowsAsync<ArgumentException>(async () => await host.ExecuteAsync("set_message", 42));
                    UnityEngine.Object.DestroyImmediate(go);
                    Assert.That((await runtime.ExecuteAsync(new CommandEnvelope("set_message"))).Succeeded, Is.True);
                }
                finally { if (go != null) UnityEngine.Object.DestroyImmediate(go); }
            }
        }
        private sealed class Context { public string Message; }
        private sealed class Handler : ICommandHandler<Context>
        {
            public IReadOnlyList<string> CommandNames { get; } = new[] { "set_message" };
            public Task<CommandResult> HandleAsync(CommandExecutionContext<Context> context, CancellationToken cancellationToken)
            { context.Application.Message = context.Command.Payload.Value<string>("message"); return Task.FromResult(CommandResult.Success()); }
        }
    }
}
