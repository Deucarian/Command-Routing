using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Deucarian.CommandRouting.Samples.Basic
{
    public sealed class BasicCommandRoutingSample :
        MonoBehaviour
    {
        private CommandRoutingRuntime<SampleContext> runtime;

        private void Awake()
        {
            var context = new SampleContext();
            runtime =
                new CommandRoutingRuntime<SampleContext>(
                    context,
                    new ICommandHandler<SampleContext>[]
                    {
                        new SetMessageHandler()
                    });
        }

        private void OnDestroy()
        {
            runtime?.Dispose();
        }

        [ContextMenu("Dispatch sample command")]
        private async void DispatchSample()
        {
            await runtime.RouteJsonAsync(
                "{\"command\":\"set_message\"," +
                "\"payload\":{\"message\":\"Hello\"}}");
        }

        private sealed class SampleContext
        {
            public string Message { get; set; }
        }

        private sealed class SetMessageHandler :
            ICommandHandler<SampleContext>
        {
            private static readonly string[] Names =
            {
                "set_message"
            };

            public IReadOnlyList<string> CommandNames =>
                Names;

            public Task<CommandResult> HandleAsync(
                CommandExecutionContext<SampleContext> context,
                CancellationToken cancellationToken)
            {
                string message =
                    context.Command.Payload
                        .Value<string>("message") ??
                    string.Empty;
                context.Application.Message = message;
                return Task.FromResult(
                    CommandResult.Success(
                        new JObject
                        {
                            ["message_length"] =
                                message.Length
                        }));
            }
        }
    }
}
