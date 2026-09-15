namespace Deucarian.CommandRouting.Samples.SimpleUsage
{
    [CommandKeySet]
    public static class CommandKeys
    {
        public static CommandKey<string> SetMessage => new Definition();
        private sealed class Definition : CommandKey<string>
        {
            public Definition() : base("set_message") { }
        }
    }
}
