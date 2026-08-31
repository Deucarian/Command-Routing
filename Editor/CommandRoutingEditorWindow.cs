using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Diagnostics;
using Deucarian.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Deucarian.CommandRouting.Editor
{
    public sealed partial class CommandRoutingEditorWindow :
        EditorWindow
    {
        private const string SettingsFolder =
            "Assets/Deucarian/CommandRouting";
        private const string SettingsPath =
            SettingsFolder +
            "/CommandRoutingSettings.asset";

        private static readonly string[] Tabs =
        {
            "Overview",
            "Settings",
            "Live Tester",
            "Diagnostics"
        };

        private CommandRoutingSettings settings;
        private SerializedObject serializedSettings;
        private Vector2 scrollPosition;
        private int selectedTab;
        private string simulatorJson =
            "{\n" +
            "  \"protocol_version\": 1,\n" +
            "  \"command_id\": \"editor-preview-1\",\n" +
            "  \"command\": \"example_command\",\n" +
            "  \"payload\": {}\n" +
            "}";
        private string simulatorResult =
            "Enter an envelope and validate it.";
        private MessageType simulatorMessageType =
            MessageType.Info;
        private string simulatorResponse = string.Empty;
        private IReadOnlyList<ICommandTestCatalogSource> catalogSources =
            new List<ICommandTestCatalogSource>();
        private CommandTestCatalog catalog;
        private string selectedCatalogSourceId = string.Empty;
        private int selectedCatalogSourceIndex;
        private int selectedScenarioIndex;
        private int observedCatalogRegistryVersion = -1;
        private bool sending;
        private CancellationTokenSource sendCancellation;
        private long lastRevision;
        private int nextCommandSequence;
        private float automaticCommandDelaySeconds = 0.75f;
        private bool showAutomatedChecks;
    }
}
