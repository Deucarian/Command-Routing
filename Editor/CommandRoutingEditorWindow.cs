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
    public sealed class CommandRoutingEditorWindow :
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

        [MenuItem(
            "Tools/Deucarian/Communication/Command Routing")]
        public static void Open()
        {
            var window =
                GetWindow<CommandRoutingEditorWindow>();
            window.titleContent =
                new GUIContent("Command Routing");
            window.minSize = new Vector2(560f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSettings();
            RefreshCatalogSources();
        }

        private void OnDisable()
        {
            sendCancellation?.Cancel();
            sendCancellation?.Dispose();
            sendCancellation = null;
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            DeucarianEditorChrome.DrawPackageHeader(
                "network",
                "Command Routing",
                "Commands, protocol, diagnostics and validation");

            selectedTab =
                GUILayout.Toolbar(selectedTab, Tabs);
            GUILayout.Space(8f);

            scrollPosition =
                EditorGUILayout.BeginScrollView(
                    scrollPosition);
            switch (selectedTab)
            {
                case 1:
                    DrawSettings();
                    break;
                case 2:
                    DrawLiveTester();
                    break;
                case 3:
                    DrawDiagnostics();
                    break;
                default:
                    DrawOverview();
                    break;
            }

            EditorGUILayout.EndScrollView();
            DeucarianEditorChrome.DrawFooterVersion(
                "Deucarian Command Routing",
                "0.2.0");
        }

        private void DrawOverview()
        {
            DeucarianEditorCards.DrawCard(
                "Operational standard",
                () =>
                {
                    EditorGUILayout.LabelField(
                        "Command Routing is transport-independent. " +
                        "Applications compose explicit handler strategies; " +
                        "browser, UDP, WebSocket, editor, test and in-process " +
                        "sources feed the same dispatcher.",
                        EditorStyles.wordWrappedLabel);
                    GUILayout.Space(6f);
                    EditorGUILayout.LabelField(
                        "Logging, diagnostics, JSON support and Deucarian " +
                        "editor styling are mandatory package capabilities.",
                        EditorStyles.wordWrappedLabel);
                });

            DeucarianEditorCards.DrawCard(
                "Current project",
                () =>
                {
                    EditorGUILayout.LabelField(
                        settings == null
                            ? "No CommandRoutingSettings asset was found."
                            : "Using " +
                              AssetDatabase.GetAssetPath(settings),
                        EditorStyles.wordWrappedLabel);
                    GUILayout.Space(6f);
                    if (settings == null &&
                        DeucarianEditorButtons.Primary(
                            "Create settings",
                            true))
                    {
                        CreateSettings();
                    }
                    else if (settings != null &&
                             DeucarianEditorButtons.Secondary(
                                 "Select settings"))
                    {
                        Selection.activeObject = settings;
                        EditorGUIUtility.PingObject(settings);
                    }
                });

            DrawValidationCard();
        }

        private void DrawSettings()
        {
            if (settings == null)
            {
                DeucarianEditorCards.DrawCard(
                    "Settings",
                    () =>
                    {
                        DeucarianEditorChrome.DrawInlineHelp(
                            "Create one settings asset to control the " +
                            "bounded operational defaults.",
                            MessageType.Info);
                        if (DeucarianEditorButtons.Primary(
                                "Create settings",
                                true))
                        {
                            CreateSettings();
                        }
                    });
                return;
            }

            serializedSettings.Update();
            DeucarianEditorCards.DrawCard(
                "Limits and history",
                () =>
                {
                    EditorGUILayout.PropertyField(
                        serializedSettings.FindProperty(
                            "historyCapacity"),
                        new GUIContent("History capacity"));
                    EditorGUILayout.PropertyField(
                        serializedSettings.FindProperty(
                            "maximumMessageCharacters"),
                        new GUIContent(
                            "Maximum message characters"));
                },
                "Sane defaults protect diagnostics and network boundaries.");

            DeucarianEditorCards.DrawCard(
                "Logging",
                () =>
                {
                    EditorGUILayout.PropertyField(
                        serializedSettings.FindProperty(
                            "logSuccessfulCommands"),
                        new GUIContent(
                            "Log successful commands"));
                    EditorGUILayout.PropertyField(
                        serializedSettings.FindProperty(
                            "logFailedCommands"),
                        new GUIContent(
                            "Log failed commands"));
                    DeucarianEditorChrome.DrawInlineHelp(
                        "Payload values are never written to command logs.",
                        MessageType.Info);
                });

            if (serializedSettings.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(settings);
            }

            DrawValidationCard();
        }

        private void DrawLiveTester()
        {
            if (observedCatalogRegistryVersion !=
                CommandTestCatalogSourceRegistry.Version)
            {
                RefreshCatalogSources();
            }

            DrawLiveRoute();
            DrawGeneratedScenarios();

            DeucarianEditorCards.DrawCard(
                "Manual JSON envelope",
                () =>
                {
                    simulatorJson =
                        EditorGUILayout.TextArea(
                            simulatorJson,
                            GUILayout.MinHeight(190f));
                    GUILayout.Space(6f);
                    EditorGUILayout.BeginHorizontal();
                    if (DeucarianEditorButtons.Primary(
                            "Validate envelope",
                            !sending))
                    {
                        ValidateSimulatorJson();
                    }

                    if (DeucarianEditorButtons.Primary(
                            sending ? "Sending..." : "Send to running route",
                            CanSendToLiveRoute()))
                    {
                        SendEnvelopeAsync(simulatorJson, null);
                    }

                    if (DeucarianEditorButtons.Secondary(
                            "Copy Python example"))
                    {
                        EditorGUIUtility.systemCopyBuffer =
                            CreatePythonExample();
                    }

                    EditorGUILayout.EndHorizontal();
                },
                "The live action routes through the same scene port used by local integrations.");

            DeucarianEditorChrome.DrawInlineHelp(
                simulatorResult,
                simulatorMessageType);
            if (!string.IsNullOrWhiteSpace(simulatorResponse))
            {
                DeucarianEditorCards.DrawCard(
                    "Latest response",
                    () => EditorGUILayout.TextArea(
                        simulatorResponse,
                        GUILayout.MinHeight(90f)));
            }
        }

        private void DrawLiveRoute()
        {
            DeucarianEditorCards.DrawCard(
                "Running command route",
                () =>
                {
                    TryResolveLiveRoute(
                        out CommandRoutePortBehaviour _,
                        out string status);
                    DeucarianEditorChrome.DrawInlineHelp(
                        status,
                        CanSendToLiveRoute()
                            ? MessageType.Info
                            : MessageType.Warning);
                },
                "Enter Play Mode and wait for exactly one initialized scene command port.");
        }

        private void DrawGeneratedScenarios()
        {
            DeucarianEditorCards.DrawCard(
                "Generated scenarios",
                () =>
                {
                    if (catalogSources.Count == 0)
                    {
                        DeucarianEditorChrome.DrawInlineHelp(
                            "No package has registered a command test catalog for the current project.",
                            MessageType.Info);
                        return;
                    }

                    string[] sourceNames = new string[catalogSources.Count];
                    for (int index = 0; index < catalogSources.Count; index++)
                    {
                        sourceNames[index] = catalogSources[index].DisplayName;
                    }

                    int sourceIndex = EditorGUILayout.Popup(
                        "Catalog",
                        selectedCatalogSourceIndex,
                        sourceNames);
                    if (sourceIndex != selectedCatalogSourceIndex)
                    {
                        selectedCatalogSourceIndex = sourceIndex;
                        selectedCatalogSourceId =
                            catalogSources[sourceIndex].Id;
                        LoadSelectedCatalog();
                    }

                    if (DeucarianEditorButtons.Secondary("Refresh scenarios"))
                    {
                        LoadSelectedCatalog();
                    }

                    if (catalog == null || catalog.Scenarios.Count == 0)
                    {
                        return;
                    }

                    string[] scenarioNames =
                        new string[catalog.Scenarios.Count];
                    for (int index = 0;
                         index < catalog.Scenarios.Count;
                         index++)
                    {
                        scenarioNames[index] = catalog.Scenarios[index].Label;
                    }

                    selectedScenarioIndex = EditorGUILayout.Popup(
                        "Scenario",
                        Math.Min(
                            selectedScenarioIndex,
                            catalog.Scenarios.Count - 1),
                        scenarioNames);
                    CommandTestScenario selected =
                        catalog.Scenarios[selectedScenarioIndex];
                    EditorGUILayout.LabelField(
                        "Command",
                        selected.CommandName);
                    EditorGUILayout.TextArea(
                        selected.Payload.ToString(Formatting.Indented),
                        GUILayout.MinHeight(80f));

                    automaticCommandDelaySeconds = EditorGUILayout.Slider(
                        "Sequence delay",
                        automaticCommandDelaySeconds,
                        0.1f,
                        2f);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (DeucarianEditorButtons.Primary(
                                sending ? "Sending..." : "Send selected",
                                CanSendToLiveRoute()))
                        {
                            SendScenarioAsync(selected);
                        }

                        if (DeucarianEditorButtons.Primary(
                                sending ? "Running..." : "Run automatic sequence",
                                CanSendToLiveRoute() &&
                                HasAutomaticScenarios()))
                        {
                            RunAutomaticSequenceAsync();
                        }
                    }
                },
                "Scenario providers own the examples; Command Routing only sends them.");
        }

        private void DrawDiagnostics()
        {
            DiagnosticReport report =
                DiagnosticProviderRegistry.BuildReport();
            var matching =
                new List<DiagnosticSection>();
            foreach (DiagnosticSection section
                     in report.Sections)
            {
                if (section.Id != null &&
                    section.Id.StartsWith(
                        "command-routing."))
                {
                    matching.Add(section);
                }
            }

            if (matching.Count == 0)
            {
                DeucarianEditorCards.DrawCard(
                    "Runtime diagnostics",
                    () => DeucarianEditorChrome.DrawInlineHelp(
                        "No active CommandRoutingRuntime is registered. " +
                        "Enter Play Mode or construct a runtime explicitly.",
                        MessageType.Info));
                return;
            }

            foreach (DiagnosticSection section in matching)
            {
                DeucarianEditorCards.DrawCard(
                    section.Title,
                    () =>
                    {
                        foreach (DiagnosticItem item
                                 in section.Items)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(
                                item.Label,
                                GUILayout.Width(190f));
                            EditorGUILayout.SelectableLabel(
                                item.Value,
                                GUILayout.Height(18f));
                            EditorGUILayout.EndHorizontal();
                        }
                    });
            }
        }

        private void DrawValidationCard()
        {
            string validation =
                CommandRoutingSettingsValidation.Validate(
                    settings);
            bool valid = string.IsNullOrEmpty(validation);
            DeucarianEditorCards.DrawCard(
                "Validation",
                () => DeucarianEditorChrome.DrawInlineHelp(
                    valid
                        ? "Command Routing settings are valid."
                        : validation,
                    valid
                        ? MessageType.Info
                        : MessageType.Warning));
        }

        private void ValidateSimulatorJson()
        {
            var codec = new JsonCommandProtocolCodec();
            if (!codec.TryDecode(
                    simulatorJson,
                    out CommandEnvelope command,
                    out CommandResult failure))
            {
                simulatorResult =
                    failure.ErrorCode +
                    ": " +
                    failure.Message;
                simulatorMessageType = MessageType.Error;
                return;
            }

            var redactor = new DefaultCommandRedactor();
            JObject sanitized =
                redactor.Redact(command.RawEnvelope)
                    as JObject;
            simulatorResult =
                "Valid command '" +
                command.CommandName +
                "'. Sanitized envelope:\n" +
                sanitized?.ToString(Formatting.Indented);
            simulatorMessageType = MessageType.Info;
        }

        private void RefreshCatalogSources()
        {
            catalogSources = CommandTestCatalogSourceRegistry.Sources;
            observedCatalogRegistryVersion =
                CommandTestCatalogSourceRegistry.Version;
            if (catalogSources.Count == 0)
            {
                selectedCatalogSourceIndex = 0;
                selectedCatalogSourceId = string.Empty;
                catalog = null;
                return;
            }

            int matchingIndex = -1;
            for (int index = 0; index < catalogSources.Count; index++)
            {
                if (string.Equals(
                        catalogSources[index].Id,
                        selectedCatalogSourceId,
                        StringComparison.Ordinal))
                {
                    matchingIndex = index;
                    break;
                }
            }

            selectedCatalogSourceIndex = matchingIndex >= 0
                ? matchingIndex
                : 0;
            selectedCatalogSourceId =
                catalogSources[selectedCatalogSourceIndex].Id;
            LoadSelectedCatalog();
        }

        private void LoadSelectedCatalog()
        {
            catalog = null;
            selectedScenarioIndex = 0;
            if (catalogSources.Count == 0 ||
                selectedCatalogSourceIndex < 0 ||
                selectedCatalogSourceIndex >= catalogSources.Count)
            {
                simulatorResult = "No command test catalog is available.";
                simulatorMessageType = MessageType.Info;
                return;
            }

            ICommandTestCatalogSource source =
                catalogSources[selectedCatalogSourceIndex];
            if (!source.TryGetCatalogJson(out string json, out string error))
            {
                simulatorResult = string.IsNullOrWhiteSpace(error)
                    ? "The command test catalog could not be loaded."
                    : error;
                simulatorMessageType = MessageType.Warning;
                return;
            }

            if (!CommandTestCatalog.TryParse(json, out catalog, out error))
            {
                simulatorResult = error;
                simulatorMessageType = MessageType.Error;
                return;
            }

            simulatorResult = "Loaded " + catalog.Scenarios.Count +
                              " scenarios from " + source.DisplayName +
                              " for endpoint " + catalog.RemoteEndpoint + ".";
            simulatorMessageType = MessageType.Info;
        }

        private bool HasAutomaticScenarios()
        {
            if (catalog == null)
            {
                return false;
            }

            for (int index = 0; index < catalog.Scenarios.Count; index++)
            {
                if (catalog.Scenarios[index].RunAutomatically)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanSendToLiveRoute()
        {
            return !sending && TryResolveLiveRoute(
                out CommandRoutePortBehaviour _,
                out string _);
        }

        private static bool TryResolveLiveRoute(
            out CommandRoutePortBehaviour route,
            out string status)
        {
            route = null;
            if (!EditorApplication.isPlaying)
            {
                status = "Enter Play Mode to send commands to the running application.";
                return false;
            }

            CommandRoutePortBehaviour[] candidates =
                Resources.FindObjectsOfTypeAll<CommandRoutePortBehaviour>();
            int readyCount = 0;
            for (int index = 0; index < candidates.Length; index++)
            {
                CommandRoutePortBehaviour candidate = candidates[index];
                if (candidate == null || !candidate.IsReady ||
                    !candidate.gameObject.scene.IsValid() ||
                    !candidate.gameObject.scene.isLoaded ||
                    EditorUtility.IsPersistent(candidate))
                {
                    continue;
                }

                readyCount++;
                route = candidate;
            }

            if (readyCount == 1)
            {
                status = "Ready: " + route.gameObject.name +
                         " in scene " + route.gameObject.scene.name + ".";
                return true;
            }

            route = null;
            status = readyCount == 0
                ? "Waiting for one initialized scene command port."
                : "Found " + readyCount +
                  " initialized command ports. Keep exactly one running for live testing.";
            return false;
        }

        private async void SendScenarioAsync(CommandTestScenario scenario)
        {
            if (scenario == null || sending)
            {
                return;
            }

            string envelope = CreateScenarioEnvelope(scenario);
            simulatorJson = envelope;
            await RunSingleSendAsync(
                envelope,
                scenario.Label,
                scenario.ExpectedSuccess);
        }

        private async void SendEnvelopeAsync(
            string envelope,
            bool? expectedSuccess)
        {
            if (sending)
            {
                return;
            }

            await RunSingleSendAsync(
                envelope,
                "Manual command",
                expectedSuccess);
        }

        private async Task RunSingleSendAsync(
            string envelope,
            string label,
            bool? expectedSuccess)
        {
            BeginSending();
            try
            {
                CommandRouteOutcome outcome = await RouteAsync(
                    envelope,
                    ResolveRemoteEndpoint(),
                    sendCancellation.Token);
                ApplyOutcome(label, outcome, expectedSuccess);
            }
            catch (OperationCanceledException)
            {
                simulatorResult = "Command testing was cancelled.";
                simulatorMessageType = MessageType.Info;
            }
            catch (Exception exception)
            {
                simulatorResult = "Command testing failed with " +
                                  exception.GetType().Name + ".";
                simulatorMessageType = MessageType.Error;
            }
            finally
            {
                EndSending();
            }
        }

        private async void RunAutomaticSequenceAsync()
        {
            if (sending || catalog == null)
            {
                return;
            }

            BeginSending();
            int completed = 0;
            try
            {
                for (int index = 0; index < catalog.Scenarios.Count; index++)
                {
                    CommandTestScenario scenario = catalog.Scenarios[index];
                    if (!scenario.RunAutomatically)
                    {
                        continue;
                    }

                    string envelope = CreateScenarioEnvelope(scenario);
                    simulatorJson = envelope;
                    simulatorResult = "Running " + scenario.Label + "...";
                    simulatorMessageType = MessageType.Info;
                    Repaint();

                    CommandRouteOutcome outcome = await RouteAsync(
                        envelope,
                        ResolveRemoteEndpoint(),
                        sendCancellation.Token);
                    bool matched = outcome?.Result?.Succeeded ==
                                   scenario.ExpectedSuccess;
                    ApplyOutcome(
                        scenario.Label,
                        outcome,
                        scenario.ExpectedSuccess);
                    if (!matched)
                    {
                        simulatorResult = "Automatic sequence stopped at '" +
                                          scenario.Label + "'. " +
                                          simulatorResult;
                        return;
                    }

                    completed++;
                    await Task.Delay(
                        Math.Max(
                            100,
                            (int)(automaticCommandDelaySeconds * 1000f)),
                        sendCancellation.Token);
                }

                simulatorResult = "Automatic sequence completed: " +
                                  completed + " scenarios matched expectations.";
                simulatorMessageType = MessageType.Info;
            }
            catch (OperationCanceledException)
            {
                simulatorResult = "Automatic command testing was cancelled.";
                simulatorMessageType = MessageType.Info;
            }
            catch (Exception exception)
            {
                simulatorResult = "Automatic command testing failed with " +
                                  exception.GetType().Name + ".";
                simulatorMessageType = MessageType.Error;
            }
            finally
            {
                EndSending();
            }
        }

        private string CreateScenarioEnvelope(CommandTestScenario scenario)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            lastRevision = Math.Max(timestamp, lastRevision + 1L);
            nextCommandSequence++;
            return CommandTestEnvelopeBuilder.Create(
                scenario,
                lastRevision,
                0L,
                "unity-editor-" + nextCommandSequence);
        }

        private string ResolveRemoteEndpoint() =>
            catalog == null || string.IsNullOrWhiteSpace(catalog.RemoteEndpoint)
                ? CommandTestCatalog.DefaultRemoteEndpoint
                : catalog.RemoteEndpoint;

        private static async Task<CommandRouteOutcome> RouteAsync(
            string envelope,
            string remoteEndpoint,
            CancellationToken cancellationToken)
        {
            if (!TryResolveLiveRoute(
                    out CommandRoutePortBehaviour route,
                    out string status))
            {
                return new CommandRouteOutcome(
                    null,
                    CommandResult.Failure(
                        CommandRoutingErrorCodes.RouteUnavailable,
                        status),
                    string.Empty);
            }

            return await route.RouteMessageAsync(
                envelope,
                "editor-local",
                remoteEndpoint,
                cancellationToken);
        }

        private void ApplyOutcome(
            string label,
            CommandRouteOutcome outcome,
            bool? expectedSuccess)
        {
            CommandResult result = outcome?.Result ??
                CommandResult.Failure(
                    CommandRoutingErrorCodes.RouteUnavailable,
                    "The command route returned no result.");
            bool matched = !expectedSuccess.HasValue ||
                           result.Succeeded == expectedSuccess.Value;
            string actual = result.Succeeded
                ? "succeeded"
                : "failed" +
                  (string.IsNullOrWhiteSpace(result.ErrorCode)
                      ? string.Empty
                      : " with '" + result.ErrorCode + "'");
            simulatorResult = label + " " + actual + "." +
                              (string.IsNullOrWhiteSpace(result.Message)
                                  ? string.Empty
                                  : " " + result.Message);
            if (expectedSuccess.HasValue && !matched)
            {
                simulatorResult += expectedSuccess.Value
                    ? " Success was expected."
                    : " Failure was expected.";
            }

            simulatorMessageType = matched
                ? MessageType.Info
                : MessageType.Error;
            simulatorResponse = outcome?.Response ?? string.Empty;
            Repaint();
        }

        private void BeginSending()
        {
            sendCancellation?.Cancel();
            sendCancellation?.Dispose();
            sendCancellation = new CancellationTokenSource();
            sending = true;
            simulatorResponse = string.Empty;
            Repaint();
        }

        private void EndSending()
        {
            sendCancellation?.Dispose();
            sendCancellation = null;
            sending = false;
            Repaint();
        }

        private void RefreshSettings()
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:CommandRoutingSettings");
            settings =
                guids.Length == 0
                    ? null
                    : AssetDatabase.LoadAssetAtPath<
                        CommandRoutingSettings>(
                        AssetDatabase.GUIDToAssetPath(
                            guids[0]));
            serializedSettings =
                settings == null
                    ? null
                    : new SerializedObject(settings);
        }

        private void CreateSettings()
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }

            settings =
                CreateInstance<CommandRoutingSettings>();
            AssetDatabase.CreateAsset(
                settings,
                AssetDatabase.GenerateUniqueAssetPath(
                    SettingsPath));
            AssetDatabase.SaveAssets();
            RefreshSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private static string CreatePythonExample()
        {
            return
                "import json\n\n" +
                "command = {\n" +
                "    \"protocol_version\": 1,\n" +
                "    \"command_id\": \"python-1\",\n" +
                "    \"command\": \"example_command\",\n" +
                "    \"payload\": {},\n" +
                "    \"metadata\": {\"source\": \"python\"},\n" +
                "}\n\n" +
                "message = json.dumps(command)\n";
        }
    }
}
