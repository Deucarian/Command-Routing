using System;
using Newtonsoft.Json.Linq;

namespace Deucarian.CommandRouting
{
    /// <summary>Explicit, reflection-free mapping from a caller's value to an existing command payload.</summary>
    public abstract class CommandPayloadBinding
    {
        protected CommandPayloadBinding(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName)) throw new ArgumentException("A command name is required.", nameof(commandName));
            CommandName = commandName.Trim();
        }
        public string CommandName { get; }
        public static CommandPayloadBinding For<T>(string commandName, Func<T, JObject> encode) => new Typed<T>(commandName, encode);
        internal sealed class Typed<T> : CommandPayloadBinding
        {
            private readonly Func<T, JObject> encode;
            public Typed(string commandName, Func<T, JObject> encode) : base(commandName)
            { this.encode = encode ?? throw new ArgumentNullException(nameof(encode)); }
            public JObject Encode(T value) => encode(value);
        }
    }
}
