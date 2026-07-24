using System.Collections.Generic;
using System.IO;
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
            "Simulator",
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
                    DrawSimulator();
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
                "0.1.0");
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

        private void DrawSimulator()
        {
            DeucarianEditorCards.DrawCard(
                "Sanitized JSON envelope",
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
                            true))
                    {
                        ValidateSimulatorJson();
                    }

                    if (DeucarianEditorButtons.Secondary(
                            "Copy Python example"))
                    {
                        EditorGUIUtility.systemCopyBuffer =
                            CreatePythonExample();
                    }

                    EditorGUILayout.EndHorizontal();
                },
                "Validation never dispatches application behavior.");

            DeucarianEditorChrome.DrawInlineHelp(
                simulatorResult,
                simulatorMessageType);
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
