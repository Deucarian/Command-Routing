using System;
using Newtonsoft.Json.Linq;

namespace Deucarian.CommandRouting
{
    public sealed class DefaultCommandRedactor :
        ICommandRedactor
    {
        private static readonly string[] SensitiveFragments =
        {
            "token",
            "password",
            "secret",
            "authorization",
            "credential",
            "api_key",
            "apikey"
        };

        public JToken Redact(JToken value)
        {
            if (value == null)
            {
                return JValue.CreateNull();
            }

            JToken clone = value.DeepClone();
            RedactInPlace(clone);
            return clone;
        }

        public bool IsSensitiveProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            string normalized =
                propertyName.Trim().ToLowerInvariant();
            for (int index = 0;
                 index < SensitiveFragments.Length;
                 index++)
            {
                if (normalized.Contains(
                        SensitiveFragments[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private void RedactInPlace(JToken token)
        {
            if (token is JObject objectValue)
            {
                foreach (JProperty property
                         in objectValue.Properties())
                {
                    if (IsSensitiveProperty(property.Name))
                    {
                        property.Value = "***REDACTED***";
                        continue;
                    }

                    RedactInPlace(property.Value);
                }

                return;
            }

            if (token is JArray arrayValue)
            {
                foreach (JToken item in arrayValue)
                {
                    RedactInPlace(item);
                }
            }
        }
    }
}
