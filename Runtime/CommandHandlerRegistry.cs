using System;
using System.Collections.Generic;

namespace Deucarian.CommandRouting
{
    public sealed class CommandHandlerRegistry<TApplicationContext>
    {
        private readonly Dictionary<
            string,
            ICommandHandler<TApplicationContext>> handlers;

        private readonly ICommandNameNormalizer normalizer;
        private readonly int handlerCount;

        public CommandHandlerRegistry(
            IEnumerable<ICommandHandler<TApplicationContext>>
                commandHandlers,
            ICommandNameNormalizer commandNameNormalizer = null)
        {
            if (commandHandlers == null)
            {
                throw new ArgumentNullException(
                    nameof(commandHandlers));
            }

            normalizer =
                commandNameNormalizer ??
                new DefaultCommandNameNormalizer();
            handlers =
                new Dictionary<
                    string,
                    ICommandHandler<TApplicationContext>>(
                    StringComparer.Ordinal);

            var uniqueHandlers =
                new HashSet<ICommandHandler<TApplicationContext>>();
            foreach (ICommandHandler<TApplicationContext> handler
                     in commandHandlers)
            {
                Register(handler);
                uniqueHandlers.Add(handler);
            }

            handlerCount = uniqueHandlers.Count;
        }

        public int HandlerCount => handlerCount;
        public int CommandNameCount => handlers.Count;

        public IReadOnlyList<string> CommandNames
        {
            get
            {
                var names = new List<string>(handlers.Keys);
                names.Sort(StringComparer.Ordinal);
                return names;
            }
        }

        public bool TryResolve(
            string commandName,
            out ICommandHandler<TApplicationContext> handler,
            out string normalizedCommandName)
        {
            normalizedCommandName =
                normalizer.Normalize(commandName);
            return handlers.TryGetValue(
                normalizedCommandName,
                out handler);
        }

        private void Register(
            ICommandHandler<TApplicationContext> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            IReadOnlyList<string> names = handler.CommandNames;
            if (names == null || names.Count == 0)
            {
                throw new ArgumentException(
                    "A command handler requires at least one command name.",
                    nameof(handler));
            }

            for (int index = 0; index < names.Count; index++)
            {
                string name = normalizer.Normalize(names[index]);
                if (name.Length == 0)
                {
                    throw new ArgumentException(
                        "Command names cannot be empty.",
                        nameof(handler));
                }

                if (handlers.ContainsKey(name))
                {
                    throw new InvalidOperationException(
                        "Duplicate command handler registration for '" +
                        name +
                        "'.");
                }

                handlers.Add(name, handler);
            }
        }
    }
}
