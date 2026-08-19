using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Deucarian.CommandRouting
{
    /// <summary>
    /// Scene-owned bridge to an explicitly injected command route port.
    /// This component owns no application or global registry state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommandRoutePortBehaviour : MonoBehaviour,
        ICommandRoutePort
    {
        private ICommandRoutePort routePort;

        public bool IsReady => routePort != null;

        public void Initialize(ICommandRoutePort port)
        {
            if (port == null)
            {
                throw new ArgumentNullException(nameof(port));
            }

            if (ReferenceEquals(port, this))
            {
                throw new ArgumentException(
                    "A command route port behaviour cannot route to itself.",
                    nameof(port));
            }

            routePort = port;
        }

        public void Clear(ICommandRoutePort expectedPort = null)
        {
            if (expectedPort == null || ReferenceEquals(routePort, expectedPort))
            {
                routePort = null;
            }
        }

        public Task<CommandRouteOutcome> RouteMessageAsync(
            string message,
            string transport = null,
            string remoteEndpoint = null,
            CancellationToken cancellationToken = default)
        {
            ICommandRoutePort current = routePort;
            if (current != null)
            {
                return current.RouteMessageAsync(
                    message,
                    transport,
                    remoteEndpoint,
                    cancellationToken);
            }

            return Task.FromResult(
                new CommandRouteOutcome(
                    null,
                    CommandResult.Failure(
                        CommandRoutingErrorCodes.RouteUnavailable,
                        "No command route has been composed for this scene port."),
                    string.Empty));
        }
    }
}
