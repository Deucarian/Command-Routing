namespace Deucarian.CommandRouting
{
    public interface ICommandProtocolCodec
    {
        bool TryDecode(
            string message,
            out CommandEnvelope command,
            out CommandResult failure);

        string EncodeResult(
            CommandEnvelope command,
            CommandResult result);
    }
}
