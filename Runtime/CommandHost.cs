using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Deucarian.CommandRouting
{
    /// <summary>Scoped convenience adapter over an explicitly composed runtime and immutable payload bindings.</summary>
    [DisallowMultipleComponent]
    public sealed class CommandHost : MonoBehaviour
    {
        private Dictionary<string, CommandPayloadBinding> bindings;
        private Func<CommandEnvelope, CancellationToken, Task<CommandResult>> dispatch;
        private IDisposable ownedRuntime;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool destroyed;

        public void Configure<TContext>(CommandRoutingRuntime<TContext> runtime,
            IEnumerable<CommandPayloadBinding> payloadBindings, bool takeOwnership = false)
        {
            if (destroyed) throw new ObjectDisposedException(nameof(CommandHost));
            if (dispatch != null) throw new InvalidOperationException("The command host is already configured.");
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (payloadBindings == null) throw new ArgumentNullException(nameof(payloadBindings));
            var copy = new Dictionary<string, CommandPayloadBinding>(StringComparer.Ordinal);
            foreach (var binding in payloadBindings)
            {
                if (binding == null) throw new ArgumentException("A payload binding cannot be null.", nameof(payloadBindings));
                copy.Add(binding.CommandName, binding);
            }
            bindings = copy;
            dispatch = runtime.ExecuteAsync;
            if (takeOwnership) ownedRuntime = runtime;
        }

        public async Task<CommandResult> ExecuteAsync<T>(string name, T payload, CancellationToken cancellationToken = default)
        {
            if (destroyed) throw new ObjectDisposedException(nameof(CommandHost));
            if (dispatch == null) throw new InvalidOperationException("Configure the command host first.");
            if (!bindings.TryGetValue(name, out var binding))
                return CommandResult.Failure(CommandRoutingErrorCodes.UnsupportedCommand, "No direct payload binding is registered for this command.");
            if (!(binding is CommandPayloadBinding.Typed<T> typed))
                throw new ArgumentException("The payload type does not match the registered command binding.", nameof(payload));
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken))
            {
                cancellation.Token.ThrowIfCancellationRequested();
                return await dispatch(new CommandEnvelope(binding.CommandName, typed.Encode(payload)), cancellation.Token);
            }
        }

        private void OnDestroy()
        {
            destroyed = true;
            lifetime.Cancel();
            lifetime.Dispose();
            ownedRuntime?.Dispose();
            bindings?.Clear();
            dispatch = null;
        }
    }
}
