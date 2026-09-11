namespace Deucarian.CommandRouting.Tests
{
    internal sealed class CommandHostTestsKey<T> : CommandKey<T>
    {
        public CommandHostTestsKey(string id) : base(id) { }
    }
}
