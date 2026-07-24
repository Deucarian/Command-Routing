using Newtonsoft.Json.Linq;

namespace Deucarian.CommandRouting
{
    public interface ICommandRedactor
    {
        JToken Redact(JToken value);
        bool IsSensitiveProperty(string propertyName);
    }
}
