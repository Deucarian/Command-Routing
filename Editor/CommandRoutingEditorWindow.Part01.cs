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
    public sealed partial class CommandRoutingEditorWindow
    {


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
                "com.deucarian.command-routing");
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

            DrawCommandComposer();

            DeucarianEditorChrome.DrawInlineHelp(
                simulatorResult,
                simulatorMessageType);
            if (!string.IsNullOrWhiteSpace(simulatorResponse))
            {
                DeucarianEditorCards.DrawCard(
                    "Latest response",
                    () => EditorGUILayout.TextArea(
                        simulatorResponse,
                        GUILayout.MinHeight(72f)));
            }

            GUILayout.Space(4f);
            showAutomatedChecks = EditorGUILayout.Foldout(
                showAutomatedChecks,
                "Automated checks",
                true);
            if (showAutomatedChecks)
            {
                DrawAutomatedChecks();
            }
        }

        private void DrawCommandComposer()
        {
            DeucarianEditorCards.DrawCard(
                "Backoffice command",
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
                    GUILayout.Space(6f);
                    DrawExamplePicker();
                    GUILayout.Space(6f);
                    EditorGUILayout.LabelField(
                        "Exact JSON envelope",
                        EditorStyles.miniLabel);
                    simulatorJson =
                        EditorGUILayout.TextArea(
                            simulatorJson,
                            GUILayout.MinHeight(180f));
                    GUILayout.Space(8f);
                    EditorGUILayout.BeginHorizontal();
                    if (DeucarianEditorButtons.Primary(
                            sending ? "Sending..." : "Send command",
                            CanSendToLiveRoute()))
                    {
                        SendEnvelopeAsync(simulatorJson, null);
                    }

                    if (DeucarianEditorButtons.Secondary(
                            "Validate JSON"))
                    {
                        ValidateSimulatorJson();
                    }

                    EditorGUILayout.EndHorizontal();
                },
                "Paste the complete envelope sent by the host. It is routed unchanged to the running viewer.");
        }

        private void DrawExamplePicker()
        {
            if (catalogSources.Count == 0 ||
                catalog == null ||
                catalog.Scenarios.Count == 0)
            {
                return;
            }

            string[] scenarioNames = new string[catalog.Scenarios.Count];
            for (int index = 0; index < catalog.Scenarios.Count; index++)
            {
                scenarioNames[index] = catalog.Scenarios[index].Label;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                selectedScenarioIndex = EditorGUILayout.Popup(
                    "Example",
                    Math.Min(
                        selectedScenarioIndex,
                        catalog.Scenarios.Count - 1),
                    scenarioNames);
                if (DeucarianEditorButtons.Secondary("Load example"))
                {
                    LoadSelectedScenarioIntoEditor();
                }
            }
        }

        private void DrawAutomatedChecks()
        {
            DeucarianEditorCards.DrawCard(
                "Generated test sequence",
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

                    automaticCommandDelaySeconds = EditorGUILayout.Slider(
                        "Delay between commands",
                        automaticCommandDelaySeconds,
                        0.1f,
                        2f);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (DeucarianEditorButtons.Primary(
                                sending ? "Running..." : "Run all checks",
                                CanSendToLiveRoute() &&
                                HasAutomaticScenarios()))
                        {
                            RunAutomaticSequenceAsync();
                        }
                    }
                },
                "Runs the package-provided command flow against the same live viewer route.");
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
    }
}
