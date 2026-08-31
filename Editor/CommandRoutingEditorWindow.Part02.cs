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

            selectedScenarioIndex = catalog.ResolveDefaultScenarioIndex();
            if (selectedScenarioIndex >= 0)
            {
                LoadSelectedScenarioIntoEditor();
                return;
            }

            simulatorResult = "No command examples are available from " +
                              source.DisplayName + ".";
            simulatorMessageType = MessageType.Info;
        }

        private void LoadSelectedScenarioIntoEditor()
        {
            if (catalog == null || catalog.Scenarios.Count == 0)
            {
                return;
            }

            selectedScenarioIndex = Math.Max(
                0,
                Math.Min(
                    selectedScenarioIndex,
                    catalog.Scenarios.Count - 1));
            CommandTestScenario scenario =
                catalog.Scenarios[selectedScenarioIndex];
            simulatorJson = CreateScenarioEnvelope(scenario);
            simulatorResult = "Ready to send '" + scenario.Label + "'.";
            simulatorMessageType = MessageType.Info;
            simulatorResponse = string.Empty;
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
    }
}
