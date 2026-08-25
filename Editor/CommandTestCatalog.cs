using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Deucarian.CommandRouting.Editor
{
    [Serializable]
    public sealed class CommandTestScenario
    {
        [JsonProperty("id")]
        public string Id { get; private set; }

        [JsonProperty("label")]
        public string Label { get; private set; }

        [JsonProperty("command")]
        public string CommandName { get; private set; }

        [JsonProperty("payload")]
        public JObject Payload { get; private set; }

        [JsonProperty("run_automatically")]
        public bool RunAutomatically { get; private set; }

        [JsonProperty("expected_success")]
        public bool ExpectedSuccess { get; private set; }

        internal bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                error = "A command test scenario has no id.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Label))
            {
                error = "Command test scenario '" + Id + "' has no label.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(CommandName))
            {
                error = "Command test scenario '" + Id + "' has no command.";
                return false;
            }

            Payload = Payload ?? new JObject();
            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class CommandTestCatalog
    {
        public const string DefaultRemoteEndpoint = "command-tester";

        [JsonProperty("schema_version")]
        public int SchemaVersion { get; private set; }

        [JsonProperty("remote_endpoint")]
        public string RemoteEndpoint { get; private set; } =
            DefaultRemoteEndpoint;

        [JsonProperty("default_scenario_id")]
        public string DefaultScenarioId { get; private set; } = string.Empty;

        [JsonProperty("scenarios")]
        public List<CommandTestScenario> Scenarios { get; private set; } =
            new List<CommandTestScenario>();

        public static bool TryParse(
            string json,
            out CommandTestCatalog catalog,
            out string error)
        {
            catalog = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "The command test catalog is empty.";
                return false;
            }

            try
            {
                catalog = JsonConvert.DeserializeObject<CommandTestCatalog>(json);
            }
            catch (JsonException exception)
            {
                error = "The command test catalog is invalid JSON: " +
                        exception.Message;
                return false;
            }

            if (catalog == null)
            {
                error = "The command test catalog could not be read.";
                return false;
            }

            if (catalog.SchemaVersion != 1)
            {
                error = "Unsupported command test catalog schema version '" +
                        catalog.SchemaVersion + "'.";
                catalog = null;
                return false;
            }

            catalog.RemoteEndpoint = string.IsNullOrWhiteSpace(
                catalog.RemoteEndpoint)
                ? DefaultRemoteEndpoint
                : catalog.RemoteEndpoint.Trim();
            catalog.DefaultScenarioId = string.IsNullOrWhiteSpace(
                catalog.DefaultScenarioId)
                ? string.Empty
                : catalog.DefaultScenarioId.Trim();

            catalog.Scenarios = catalog.Scenarios ??
                                new List<CommandTestScenario>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.Scenarios.Count; index++)
            {
                CommandTestScenario scenario = catalog.Scenarios[index];
                if (scenario == null)
                {
                    error = "The command test catalog contains a null scenario.";
                    catalog = null;
                    return false;
                }

                if (!scenario.TryValidate(out error))
                {
                    catalog = null;
                    return false;
                }

                if (!ids.Add(scenario.Id))
                {
                    error = "Duplicate command test scenario id '" +
                            scenario.Id + "'.";
                    catalog = null;
                    return false;
                }
            }

            if (catalog.DefaultScenarioId.Length > 0 &&
                !ids.Contains(catalog.DefaultScenarioId))
            {
                error = "Default command test scenario '" +
                        catalog.DefaultScenarioId + "' does not exist.";
                catalog = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public int ResolveDefaultScenarioIndex()
        {
            if (Scenarios == null || Scenarios.Count == 0)
            {
                return -1;
            }

            if (DefaultScenarioId.Length == 0)
            {
                return 0;
            }

            for (int index = 0; index < Scenarios.Count; index++)
            {
                if (string.Equals(
                        Scenarios[index].Id,
                        DefaultScenarioId,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return 0;
        }
    }

    public interface ICommandTestCatalogSource
    {
        string Id { get; }
        string DisplayName { get; }

        bool TryGetCatalogJson(out string json, out string error);
    }

    public static class CommandTestCatalogSourceRegistry
    {
        private static readonly SortedDictionary<string, ICommandTestCatalogSource>
            RegisteredSources =
                new SortedDictionary<string, ICommandTestCatalogSource>(
                    StringComparer.Ordinal);

        public static int Version { get; private set; }

        public static IReadOnlyList<ICommandTestCatalogSource> Sources =>
            new List<ICommandTestCatalogSource>(RegisteredSources.Values);

        public static void Register(ICommandTestCatalogSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrWhiteSpace(source.Id) ||
                string.IsNullOrWhiteSpace(source.DisplayName))
            {
                throw new ArgumentException(
                    "A command test catalog source requires an id and display name.",
                    nameof(source));
            }

            RegisteredSources[source.Id.Trim()] = source;
            Version++;
        }

        public static void Unregister(string id)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                !RegisteredSources.Remove(id.Trim()))
            {
                return;
            }

            Version++;
        }

        /// <summary>
        /// Notifies editor consumers that one or more registered catalog
        /// sources now produce different scenario data.
        /// </summary>
        public static void NotifyCatalogChanged()
        {
            Version++;
        }
    }

    public static class CommandTestEnvelopeBuilder
    {
        public static string Create(
            CommandTestScenario scenario,
            long revision,
            long staleRevision,
            string commandId)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            if (revision <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            if (string.IsNullOrWhiteSpace(commandId))
            {
                throw new ArgumentException(
                    "A command id is required.",
                    nameof(commandId));
            }

            JToken payload = (scenario.Payload ?? new JObject()).DeepClone();
            ReplaceTokens(payload, revision, Math.Max(0L, staleRevision));
            return new JObject
            {
                ["protocol_version"] = 1,
                ["command_id"] = commandId.Trim(),
                ["command"] = scenario.CommandName,
                ["payload"] = payload,
                ["metadata"] = new JObject
                {
                    ["source"] = "unity-editor-command-tester"
                }
            }.ToString(Formatting.Indented);
        }

        private static void ReplaceTokens(
            JToken token,
            long revision,
            long staleRevision)
        {
            if (token == null)
            {
                return;
            }

            if (token is JValue value && value.Type == JTokenType.String)
            {
                string text = value.Value<string>();
                if (string.Equals(text, "$revision", StringComparison.Ordinal))
                {
                    value.Replace(new JValue(revision));
                }
                else if (string.Equals(
                             text,
                             "$stale_revision",
                             StringComparison.Ordinal))
                {
                    value.Replace(new JValue(staleRevision));
                }

                return;
            }

            if (!(token is JContainer container))
            {
                return;
            }

            var children = new List<JToken>(container.Children());
            for (int index = 0; index < children.Count; index++)
            {
                ReplaceTokens(children[index], revision, staleRevision);
            }
        }
    }
}
