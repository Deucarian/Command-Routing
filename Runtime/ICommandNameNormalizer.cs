namespace Deucarian.CommandRouting
{
    public interface ICommandNameNormalizer
    {
        string Normalize(string commandName);
    }

    public sealed class DefaultCommandNameNormalizer :
        ICommandNameNormalizer
    {
        public string Normalize(string commandName)
        {
            return string.IsNullOrWhiteSpace(commandName)
                ? string.Empty
                : commandName.Trim().ToLowerInvariant();
        }
    }
}
