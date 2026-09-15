namespace Deucarian.CommandRouting.Samples.DefinitionWorkflow
{
    [CommandKeySet] public static class SampleCommands
    {
        public static CommandKey<string> SetMessage => new Key();
        private sealed class Key : CommandKey<string> { public Key() : base("workflow_set_message") { } }
    }
}
