using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.CommandRouting
{
    /// <summary>
    /// Non-generic ingress contract for explicitly composed command runtimes.
    /// It allows local editor tools and transport adapters to route the same
    /// protocol message without knowing an application's context type.
    /// </summary>
    public interface ICommandRoutePort
    {
        Task<CommandRouteOutcome> RouteMessageAsync(
            string message,
            string transport = null,
            string remoteEndpoint = null,
            CancellationToken cancellationToken = default);
    }
}
