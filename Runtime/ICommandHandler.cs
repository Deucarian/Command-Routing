using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.CommandRouting
{
    public interface ICommandHandler<TApplicationContext>
    {
        IReadOnlyList<string> CommandNames { get; }

        Task<CommandResult> HandleAsync(
            CommandExecutionContext<TApplicationContext> context,
            CancellationToken cancellationToken);
    }
}
