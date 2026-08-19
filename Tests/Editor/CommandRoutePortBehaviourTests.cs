using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Deucarian.CommandRouting.Tests
{
    public sealed class CommandRoutePortBehaviourTests
    {
        [Test]
        public async Task UninitializedPortReturnsSanitizedFailure()
        {
            GameObject owner = new GameObject("Command route port test");
            try
            {
                CommandRoutePortBehaviour behaviour =
                    owner.AddComponent<CommandRoutePortBehaviour>();

                CommandRouteOutcome outcome =
                    await behaviour.RouteMessageAsync("{\"command\":\"initialize_viewer\"}");

                Assert.That(outcome.Result.Succeeded, Is.False);
                Assert.That(
                    outcome.Result.ErrorCode,
                    Is.EqualTo(CommandRoutingErrorCodes.RouteUnavailable));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public async Task InitializedPortForwardsProtocolMetadata()
        {
            GameObject owner = new GameObject("Command route port test");
            try
            {
                var target = new RecordingRoutePort();
                CommandRoutePortBehaviour behaviour =
                    owner.AddComponent<CommandRoutePortBehaviour>();
                behaviour.Initialize(target);

                CommandRouteOutcome outcome = await behaviour.RouteMessageAsync(
                    "{\"command\":\"initialize_viewer\"}",
                    "editor-local",
                    "development-profile");

                Assert.That(outcome.Result.Succeeded, Is.True);
                Assert.That(target.Message, Does.Contain("initialize_viewer"));
                Assert.That(target.Transport, Is.EqualTo("editor-local"));
                Assert.That(target.RemoteEndpoint, Is.EqualTo("development-profile"));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private sealed class RecordingRoutePort : ICommandRoutePort
        {
            public string Message { get; private set; }
            public string Transport { get; private set; }
            public string RemoteEndpoint { get; private set; }

            public Task<CommandRouteOutcome> RouteMessageAsync(
                string message,
                string transport = null,
                string remoteEndpoint = null,
                CancellationToken cancellationToken = default)
            {
                Message = message;
                Transport = transport;
                RemoteEndpoint = remoteEndpoint;
                return Task.FromResult(
                    new CommandRouteOutcome(
                        null,
                        CommandResult.Success(),
                        string.Empty));
            }
        }
    }
}
