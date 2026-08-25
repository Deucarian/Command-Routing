using Deucarian.CommandRouting.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Deucarian.CommandRouting.Tests
{
    public sealed class CommandTestCatalogTests
    {
        private const string SourceId = "tests.generated-catalog";

        [TearDown]
        public void TearDown()
        {
            CommandTestCatalogSourceRegistry.Unregister(SourceId);
        }

        [Test]
        public void ParsesGeneratedScenarioCatalog()
        {
            Assert.That(
                CommandTestCatalog.TryParse(
                    CatalogJson(),
                    out CommandTestCatalog catalog,
                    out string error),
                Is.True,
                error);

            Assert.That(catalog.SchemaVersion, Is.EqualTo(1));
            Assert.That(catalog.RemoteEndpoint, Is.EqualTo("direct"));
            Assert.That(
                catalog.DefaultScenarioId,
                Is.EqualTo("select-inspection"));
            Assert.That(catalog.ResolveDefaultScenarioIndex(), Is.Zero);
            Assert.That(catalog.Scenarios, Has.Count.EqualTo(1));
            Assert.That(
                catalog.Scenarios[0].CommandName,
                Is.EqualTo("select_activity"));
            Assert.That(catalog.Scenarios[0].RunAutomatically, Is.True);
        }

        [Test]
        public void UsesLocalEndpointWhenCatalogDoesNotDeclareOne()
        {
            string json = CatalogJson().Replace(
                "  \"remote_endpoint\": \"direct\",\n",
                string.Empty);

            Assert.That(
                CommandTestCatalog.TryParse(
                    json,
                    out CommandTestCatalog catalog,
                    out string error),
                Is.True,
                error);
            Assert.That(
                catalog.RemoteEndpoint,
                Is.EqualTo(CommandTestCatalog.DefaultRemoteEndpoint));
        }

        [Test]
        public void RejectsDuplicateScenarioIdentifiers()
        {
            string json = CatalogJson().Replace(
                "]\n}",
                ",\n" +
                "    {\n" +
                "      \"id\": \"select-inspection\",\n" +
                "      \"label\": \"Duplicate\",\n" +
                "      \"command\": \"select_activity\",\n" +
                "      \"payload\": {},\n" +
                "      \"run_automatically\": false,\n" +
                "      \"expected_success\": true\n" +
                "    }\n" +
                "  ]\n}");

            Assert.That(
                CommandTestCatalog.TryParse(json, out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("Duplicate"));
        }

        [Test]
        public void RejectsMissingDefaultScenario()
        {
            string json = CatalogJson().Replace(
                "\"default_scenario_id\": \"select-inspection\"",
                "\"default_scenario_id\": \"missing\"");

            Assert.That(
                CommandTestCatalog.TryParse(json, out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("does not exist"));
        }

        [Test]
        public void BuildsEnvelopeWithResolvedRevisionTokens()
        {
            CommandTestCatalog.TryParse(
                CatalogJson(),
                out CommandTestCatalog catalog,
                out _);

            string envelope = CommandTestEnvelopeBuilder.Create(
                catalog.Scenarios[0],
                42,
                7,
                "editor-test-1");
            JObject parsed = JObject.Parse(envelope);

            Assert.That(
                parsed.Value<string>("command"),
                Is.EqualTo("select_activity"));
            Assert.That(
                parsed["payload"]?.Value<long>("revision"),
                Is.EqualTo(42));
            Assert.That(
                parsed["metadata"]?.Value<string>("source"),
                Is.EqualTo("unity-editor-command-tester"));
        }

        [Test]
        public void RegistersPackageProvidedCatalogSources()
        {
            var source = new TestCatalogSource();
            CommandTestCatalogSourceRegistry.Register(source);

            Assert.That(
                CommandTestCatalogSourceRegistry.Sources,
                Has.Some.Property(nameof(ICommandTestCatalogSource.Id))
                    .EqualTo(SourceId));
        }

        private static string CatalogJson()
        {
            return
                "{\n" +
                "  \"schema_version\": 1,\n" +
                "  \"remote_endpoint\": \"direct\",\n" +
                "  \"default_scenario_id\": \"select-inspection\",\n" +
                "  \"scenarios\": [\n" +
                "    {\n" +
                "      \"id\": \"select-inspection\",\n" +
                "      \"label\": \"Select inspection\",\n" +
                "      \"command\": \"select_activity\",\n" +
                "      \"payload\": {\n" +
                "        \"revision\": \"$revision\",\n" +
                "        \"activity_id\": \"inspection\"\n" +
                "      },\n" +
                "      \"run_automatically\": true,\n" +
                "      \"expected_success\": true\n" +
                "    }\n" +
                "  ]\n" +
                "}";
        }

        private sealed class TestCatalogSource : ICommandTestCatalogSource
        {
            public string Id => SourceId;
            public string DisplayName => "Generated test catalog";

            public bool TryGetCatalogJson(out string json, out string error)
            {
                json = CatalogJson();
                error = string.Empty;
                return true;
            }
        }
    }
}
