# Simple usage

Create CommandRoutingRuntime<TContext> once with your explicit application context, immutable handlers, and middleware. Configure CommandHost with that runtime and an explicit payload binding:

```csharp
CommandPayloadBinding.For<string>("set_message",
    value => new Newtonsoft.Json.Linq.JObject { ["message"] = value });
```

The registered set_message handler reads the message field as before. Each typed binding maps to the existing command contract without runtime object mapping or handler discovery. ExecuteAsync returns the existing CommandResult and runs the same dispatcher, middleware, sanitized history, and diagnostics. Unknown bindings return an unsupported-command result; mismatched payload types fail explicitly. Set takeOwnership when the host should dispose the runtime on destruction.

Import the **Simple Usage** sample from Unity Package Manager. Its caller script is:

```csharp
using UnityEngine;

namespace Deucarian.CommandRouting.Samples.SimpleUsage
{
    public sealed class SimpleUsageExample : MonoBehaviour
    {
        [SerializeField] private CommandHost commands;
        public System.Threading.Tasks.Task SetMessage(string message) => commands.ExecuteAsync("set_message", message);
    }
}
```
