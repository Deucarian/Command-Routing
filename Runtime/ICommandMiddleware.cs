using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.CommandRouting
{
    public delegate Task<CommandResult>
        CommandHandlerDelegate<TApplicationContext>(
            CommandExecutionContext<TApplicationContext> context,
            CancellationToken cancellationToken);

    public interface ICommandMiddleware<TApplicationContext>
    {
        Task<CommandResult> InvokeAsync(
            CommandExecutionContext<TApplicationContext> context,
            CommandHandlerDelegate<TApplicationContext> next,
            CancellationToken cancellationToken);
    }
}
