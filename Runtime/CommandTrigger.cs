using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
namespace Deucarian.CommandRouting
{
    /// <summary>Derive a concrete component with your payload type. Handler composition remains explicit.</summary>
    public abstract class CommandTrigger<TPayload> : MonoBehaviour
    {
        [SerializeField] private CommandHost host;
        [SerializeField] private CommandKey<TPayload> command;
        [SerializeField] private TPayload payload;
        [SerializeField] private UnityEvent succeeded = new UnityEvent();
        [SerializeField] private UnityEvent failed = new UnityEvent();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        public CommandResult LastResult { get; private set; }
        public Task<CommandResult> ExecuteAsync(TPayload value, CancellationToken cancellationToken = default)
        {
            if (host == null) throw new InvalidOperationException("Assign a configured CommandHost to this command component.");
            return host.ExecuteAsync(command, value, cancellationToken);
        }
        public async void Execute()
        {
            CommandResult result;
            try { result = await ExecuteAsync(payload, lifetime.Token); }
            catch (OperationCanceledException) { return; }
            if (this == null) return;
            LastResult = result;
            if (result.Succeeded) succeeded.Invoke(); else failed.Invoke();
        }
        protected virtual void OnDestroy() { lifetime.Cancel(); lifetime.Dispose(); }
    }
}
