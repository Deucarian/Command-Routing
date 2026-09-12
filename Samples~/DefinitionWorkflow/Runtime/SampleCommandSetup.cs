using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Deucarian.CommandRouting.Samples.DefinitionWorkflow
{
    [DefaultExecutionOrder(-2000)]
    public sealed class SampleCommandSetup : MonoBehaviour
    {
        [SerializeField] private CommandHost host;
        private readonly Context context = new Context();
        public string Message => context.Message;
        private void Awake()
        {
            var runtime = new CommandRoutingRuntime<Context>(context, new[] { new Handler() });
            host.Configure(runtime, new[] { CommandPayloadBinding.For(SampleCommands.SetMessage, value => new JObject { ["message"] = value }) }, takeOwnership: true);
        }
        private sealed class Context { public string Message; }
        private sealed class Handler : ICommandHandler<Context>
        {
            public IReadOnlyList<string> CommandNames { get; } = new[] { SampleCommands.SetMessage.Id };
            public Task<CommandResult> HandleAsync(CommandExecutionContext<Context> context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                context.Application.Message = context.Command.Payload.Value<string>("message");
                return Task.FromResult(CommandResult.Success());
            }
        }
    }
}
