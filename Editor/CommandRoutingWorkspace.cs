using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deucarian.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Controls = Deucarian.Editor.DeucarianEditorWorkspaceControls;

namespace Deucarian.CommandRouting.Editor
{
    internal sealed class CommandRoutingWorkspace : IDisposable
    {
        private readonly CommandRoutingTestSession session;
        private readonly DeucarianEditorWorkspace workspace;
        private readonly PopupField<string> sources;
        private readonly TextField search;
        private readonly VisualElement rows;
        private readonly ScrollView details;
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        private DeucarianEditorWorkspaceForm form;
        private DeucarianEditorSerializedForm settingsForm;
        private CommandRoutingSettings settings;
        private Button send, sequence;
        private string displayedKey;
        private string sourceSignature;

        internal CommandRoutingWorkspace(VisualElement root, CommandRoutingTestSession session)
        {
            this.session = session;
            workspace = new DeucarianEditorWorkspace(root, Application.productName);
            workspace.Title.text = "Command routing";
            workspace.Subtitle.text = "Send a command. Inspect the result.";
            DeucarianEditorWorkspaceNavigation.Populate(workspace, DeucarianToolIds.CommandRouting);
            var card = Controls.Panel("command-workspace");
            card.AddToClassList("dw-card-workspace");
            var left = Controls.Scroll("command-list");
            details = Controls.Scroll("command-details");
            var split = Controls.Split(left, details);
            split.AddToClassList("dw-inset-split");
            card.Add(split); workspace.Content.Add(card);
            sources = new PopupField<string>(new List<string> { "Manual command" }, 0) { name = "command-catalog" };
            left.Add(Controls.Label("Command registry", "dw-field-label"));
            left.Add(sources);
            sources.RegisterValueChangedCallback(_ =>
            {
                session.SelectSource(sources.index < session.Sources.Count ? session.Sources[sources.index].Id : null);
                displayedKey = null; Refresh();
            });
            var searchRow = Controls.Search("command-search", "Find a command…", out search);
            searchRow.AddToClassList("dw-list-search");
            left.Add(searchRow);
            search.RegisterValueChangedCallback(_ => RefreshRows());
            rows = new VisualElement { name = "command-rows" }; left.Add(rows);
            string[] guids = AssetDatabase.FindAssets("t:CommandRoutingSettings");
            Array.Sort(guids, StringComparer.Ordinal);
            if (guids.Length > 0) settings = AssetDatabase.LoadAssetAtPath<CommandRoutingSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
            session.Changed += Refresh;
            Refresh();
        }

        internal void Refresh()
        {
            string signature = string.Join("\n", session.Sources.Select(value => value.Id + "\t" + value.DisplayName));
            if (sourceSignature != signature)
            {
                sourceSignature = signature;
                sources.choices = session.Sources.Select(value => value.DisplayName).Concat(new[] { "Manual command" }).ToList();
            }
            sources.SetValueWithoutNotify(session.Sources.FirstOrDefault(value => value.Id == session.SourceId)?.DisplayName ?? "Manual command");
            sources.SetEnabled(!session.Busy);
            string key = session.SourceId + "/" + session.SelectedScenarioId;
            if (displayedKey != key)
            {
                displayedKey = key;
                BuildDetails();
            }
            RefreshRows();
            form?.Refresh();
            send?.SetEnabled(session.CanSend);
            if (send != null) send.tooltip = session.RouteStatus;
            sequence?.SetEnabled(session.CanSend && session.Catalog?.Scenarios.Any(value => value.RunAutomatically) == true);
        }

        private void RefreshRows()
        {
            var scenarios = session.Catalog?.Scenarios ?? new List<CommandTestScenario>();
            var currentIds = new HashSet<string>(scenarios.Select(value => value.Id));
            foreach (string id in buttons.Keys.Where(id => !currentIds.Contains(id)).ToArray())
            { buttons[id].RemoveFromHierarchy(); buttons.Remove(id); }
            int rowIndex = 0;
            foreach (var scenario in scenarios)
            {
                if (!buttons.TryGetValue(scenario.Id, out var button))
                {
                    string id = scenario.Id;
                    button = Controls.Button(scenario.CommandName, () => session.SelectScenario(id));
                    button.AddToClassList("dw-list-item");
                    buttons.Add(id, button); rows.Add(button);
                }
                button.text = scenario.CommandName; button.tooltip = scenario.Label;
                if (rows.IndexOf(button) != rowIndex) rows.Insert(rowIndex, button);
                rowIndex++;
                button.SetEnabled(!session.Busy);
                button.EnableInClassList("dw-selected", scenario.Id == session.SelectedScenarioId);
                Controls.Show(button, string.IsNullOrWhiteSpace(search.value) ||
                    (scenario.CommandName + " " + scenario.Label).IndexOf(search.value, StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        private void BuildDetails()
        {
            settingsForm?.Dispose(); settingsForm = null;
            details.Clear();
            var selected = session.Selected;
            details.Add(Controls.Label(selected?.CommandName ?? "Manual command", "dw-detail-title"));
            if (selected != null) details.Add(Controls.Label(selected.Label, "dw-muted"));
            form = new DeucarianEditorWorkspaceForm(details);
            if (TryPayload(out var payload))
            {
                foreach (var property in payload.Properties().Take(8))
                {
                    string key = property.Name, label = ObjectNames.NicifyVariableName(property.Name.Replace('_', ' ').Replace('-', ' '));
                    if (property.Value.Type == JTokenType.String)
                    {
                        var field = form.Text("payload-" + key, label, () => Read(key)?.Type == JTokenType.String
                            ? Read(key).Value<string>() : string.Empty, value => Write(key, value));
                        field.isPasswordField = new DefaultCommandRedactor().IsSensitiveProperty(key);
                    }
                    else if (property.Value.Type == JTokenType.Boolean)
                        form.Toggle("payload-" + key, label, () => Read(key)?.Type == JTokenType.Boolean && Read(key).Value<bool>(), value => Write(key, value));
                    else if (property.Value.Type == JTokenType.Integer || property.Value.Type == JTokenType.Float)
                    {
                        var field = form.Text("payload-" + key, label, () => Read(key)?.ToString(Formatting.None) ?? string.Empty, value =>
                        {
                            try
                            {
                                var number = JToken.Parse(value);
                                if (number.Type == JTokenType.Integer || number.Type == JTokenType.Float) Write(key, number);
                            }
                            catch (JsonException) { }
                        });
                        field.isDelayed = true;
                    }
                }
            }
            send = Controls.IconButton("Send test command", DeucarianEditorIconIds.Send, session.Send, DeucarianEditorButtonRole.Primary);
            send.name = "command-send";
            var sendRow = Controls.EndActions(send); sendRow.AddToClassList("dw-field-actions"); details.Add(sendRow);
            details.Add(Controls.Divider());
            form.ReadOnly("command-result", "Last result", () => session.Result);
            var raw = form.Section("Advanced payload (JSON)", true);
            raw.Root.AddToClassList("dw-foldout-panel");
            raw.Text("command-json", "Envelope", () => session.Json, value => session.Json = value, true);
            raw.Action("command-validate", "Validate JSON", session.ValidateJson);
            raw.Action("command-use-payload", "Update input fields", BuildDetails);
            raw.ReadOnly("command-response", "Sanitized response", () => session.Response);
            var checks = raw.Section("Automated checks", true);
            checks.Slider("command-delay", "Delay between commands", .1f, 2, () => session.DelaySeconds, value => session.DelaySeconds = value);
            sequence = checks.Action("command-sequence", "Run all checks", session.RunSequence);
            var advanced = raw.Section("Routing settings", true);
            advanced.Asset("command-settings", "Settings asset", typeof(CommandRoutingSettings), () => settings,
                value => { settings = value as CommandRoutingSettings; BuildDetails(); });
            if (settings == null) advanced.Action("command-create-settings", "Create settings", CreateSettings);
            else
            {
                settingsForm = new DeucarianEditorSerializedForm(advanced.Root, settings);
                settingsForm.Remaining();
                advanced.Note(() => CommandRoutingSettingsValidation.Validate(settings));
            }
            advanced.Action("command-diagnostics", "Open diagnostics",
                () => DeucarianEditorNavigation.Open(workspace.Root, DeucarianToolIds.Diagnostics));
        }

        private bool TryPayload(out JObject payload)
        {
            payload = null;
            try { payload = JObject.Parse(session.Json)["payload"] as JObject; return payload != null; }
            catch (JsonException) { return false; }
        }
        private JToken Read(string key) => TryPayload(out var payload) ? payload[key] : null;
        private void Write(string key, JToken value)
        {
            try
            {
                var envelope = JObject.Parse(session.Json);
                if (envelope["payload"] is JObject payload)
                { payload[key] = value; session.Json = envelope.ToString(Formatting.Indented); }
            }
            catch (JsonException) { }
        }
        private void CreateSettings()
        {
            const string folder = "Assets/Deucarian/CommandRouting";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            settings = ScriptableObject.CreateInstance<CommandRoutingSettings>();
            AssetDatabase.CreateAsset(settings, AssetDatabase.GenerateUniqueAssetPath(folder + "/CommandRoutingSettings.asset"));
            AssetDatabase.SaveAssetIfDirty(settings);
            Selection.activeObject = settings; EditorGUIUtility.PingObject(settings);
            BuildDetails();
        }
        public void Dispose()
        { session.Changed -= Refresh; settingsForm?.Dispose(); workspace.Dispose(); }
    }
}
