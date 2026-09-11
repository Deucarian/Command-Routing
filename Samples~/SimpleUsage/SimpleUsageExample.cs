using UnityEngine;

namespace Deucarian.CommandRouting.Samples.SimpleUsage
{
    public sealed class SimpleUsageExample : MonoBehaviour
    {
        [SerializeField] private CommandKey<string> setMessage = CommandKeys.SetMessage;

        [SerializeField] private CommandHost commands;
        public System.Threading.Tasks.Task SetMessage(string message) => commands.ExecuteAsync(setMessage, message);
    }
}
