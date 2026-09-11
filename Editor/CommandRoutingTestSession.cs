using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Deucarian.CommandRouting.Editor
{
    internal sealed class CommandRoutingTestSession : IDisposable
    {
        private int observedVersion = -1;
        private long lastRevision;
        private int commandSequence;
        private int operationVersion;
        private CancellationTokenSource cancellation;
        private bool active = true;
        private bool disposed;
        internal event Action Changed;
        internal IReadOnlyList<ICommandTestCatalogSource> Sources { get; private set; } = Array.Empty<ICommandTestCatalogSource>();
        internal CommandTestCatalog Catalog { get; private set; }
        internal string SourceId { get; private set; } = string.Empty;
        internal string SelectedScenarioId { get; private set; } = string.Empty;
        internal string Json { get; set; } = "{\n  \"protocol_version\": 1,\n  \"command\": \"\",\n  \"payload\": {}\n}";
        internal string Result { get; private set; } = "No command sent yet.";
        internal string Response { get; private set; } = string.Empty;
        internal float DelaySeconds { get; set; } = .75f;
        internal bool Busy => cancellation != null;
        internal bool CanSend => active && !disposed && !Busy && TryResolveLiveRoute(out _, out _);
        internal string RouteStatus { get { TryResolveLiveRoute(out _, out string value); return value; } }
        internal CommandTestScenario Selected => Catalog?.Scenarios.Find(value => value.Id == SelectedScenarioId);
        internal CommandRoutingTestSession() => RefreshCatalogSources();

        internal void RefreshCatalogSources()
        {
            if (disposed || Busy || observedVersion == CommandTestCatalogSourceRegistry.Version) return;
            observedVersion = CommandTestCatalogSourceRegistry.Version;
            Sources = CommandTestCatalogSourceRegistry.Sources;
            string previousJson = Json, previousScenario = SelectedScenarioId;
            string previousSource = SourceId;
            SelectSource(Sources.FirstOrDefault(value => value.Id == SourceId)?.Id ?? Sources.FirstOrDefault()?.Id);
            if (SourceId == previousSource)
            {
                Json = previousJson;
                SelectedScenarioId = Catalog?.Scenarios.Any(value => value.Id == previousScenario) == true ? previousScenario : string.Empty;
            }
            Notify();
        }

        internal void SelectSource(string id)
        {
            if (Busy || disposed) return;
            SourceId = id ?? string.Empty;
            Catalog = null; SelectedScenarioId = string.Empty;
            var source = Sources.FirstOrDefault(value => value.Id == SourceId);
            if (source == null) { Notify(); return; }
            try
            {
                if (!source.TryGetCatalogJson(out string json, out string error) ||
                    !CommandTestCatalog.TryParse(json, out var catalog, out error))
                { Result = "The command catalog could not be loaded."; Notify(); return; }
                Catalog = catalog;
                int index = catalog.ResolveDefaultScenarioIndex();
                if (index >= 0) SelectScenario(catalog.Scenarios[index].Id);
            }
            catch (Exception) { Result = "The command catalog is unavailable."; }
            Notify();
        }

        internal void SelectScenario(string id)
        {
            if (Busy || disposed) return;
            SelectedScenarioId = id ?? string.Empty;
            var scenario = Selected;
            if (scenario != null) Json = Envelope(scenario);
            Result = "No command sent yet."; Response = string.Empty;
            Notify();
        }

        internal void ValidateJson()
        {
            var codec = new JsonCommandProtocolCodec();
            if (!codec.TryDecode(Json, out var command, out var failure))
            { Result = "Invalid envelope · " + failure.ErrorCode; Response = string.Empty; }
            else
            {
                Result = "Valid command · " + command.CommandName;
                Response = new DefaultCommandRedactor().Redact(command.RawEnvelope).ToString(Formatting.Indented);
            }
            Notify();
        }

        internal void Send() { if (CanSend) RunAsync(false); }
        internal void RunSequence() { if (CanSend && Catalog?.Scenarios.Any(value => value.RunAutomatically) == true) RunAsync(true); }
        private async void RunAsync(bool sequence)
        {
            var lease = new CancellationTokenSource();
            cancellation = lease;
            int version = ++operationVersion;
            string remote = Catalog?.RemoteEndpoint ?? CommandTestCatalog.DefaultRemoteEndpoint;
            var scenarios = sequence ? Catalog.Scenarios.Where(value => value.RunAutomatically).ToArray() : Array.Empty<CommandTestScenario>();
            string manual = Json;
            Response = string.Empty; Result = sequence ? "Running checks…" : "Sending…"; Notify();
            try
            {
                int count = sequence ? scenarios.Length : 1;
                for (int index = 0; index < count; index++)
                {
                    lease.Token.ThrowIfCancellationRequested();
                    string envelope = sequence ? Envelope(scenarios[index]) : manual;
                    var outcome = await RouteAsync(envelope, remote, lease.Token);
                    if (!active || disposed || version != operationVersion || lease.IsCancellationRequested) return;
                    bool succeeded = outcome?.Result?.Succeeded == true;
                    bool matched = !sequence || succeeded == scenarios[index].ExpectedSuccess;
                    Result = (sequence ? scenarios[index].Label + " · " : string.Empty) +
                        (succeeded ? "Succeeded" : "Failed · " + (outcome?.Result?.ErrorCode ?? CommandRoutingErrorCodes.RouteUnavailable));
                    Response = SanitizeResponse(outcome?.Response);
                    Notify();
                    if (sequence && !matched) { Result += " · Stopped: unexpected result."; return; }
                    if (sequence && index + 1 < count)
                        await Task.Delay(DelayMilliseconds(DelaySeconds), lease.Token);
                }
                if (sequence) Result = scenarios.Length + " checks matched expectations.";
            }
            catch (OperationCanceledException) { if (version == operationVersion) Result = "Command testing canceled."; }
            catch (Exception) { if (version == operationVersion) Result = "Command testing failed. Check the live route."; }
            finally
            {
                if (ReferenceEquals(cancellation, lease)) cancellation = null;
                lease.Dispose();
                Notify();
            }
        }

        private string Envelope(CommandTestScenario scenario)
        {
            lastRevision = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), lastRevision + 1);
            return CommandTestEnvelopeBuilder.Create(scenario, lastRevision, 0, "unity-editor-" + ++commandSequence);
        }

        internal static string SanitizeResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return string.Empty;
            try { return new DefaultCommandRedactor().Redact(JToken.Parse(response)).ToString(Formatting.Indented); }
            catch (JsonException) { return "The response was not JSON; raw response omitted."; }
        }

        internal static int DelayMilliseconds(float seconds) => float.IsNaN(seconds) || float.IsInfinity(seconds)
            ? 750 : Mathf.RoundToInt(Mathf.Clamp(seconds, .1f, 2) * 1000);

        internal void Activate() { if (!disposed) { active = true; Notify(); } }
        internal void Deactivate()
        {
            active = false; operationVersion++;
            cancellation?.Cancel();
            if (Busy) Result = "Command testing canceled.";
        }
        private void Notify() { if (active && !disposed) Changed?.Invoke(); }
        public void Dispose() { Deactivate(); disposed = true; Changed = null; }

        private static bool TryResolveLiveRoute(out CommandRoutePortBehaviour route, out string status)
        {
            route = null;
            if (!EditorApplication.isPlaying) { status = "Start Play Mode to send commands to your app."; return false; }
            int count = 0;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<CommandRoutePortBehaviour>())
            {
                if (candidate == null || !candidate.IsReady || !candidate.gameObject.scene.IsValid() ||
                    !candidate.gameObject.scene.isLoaded || EditorUtility.IsPersistent(candidate)) continue;
                count++; route = candidate;
            }
            if (count == 1) { status = "Connected · " + route.gameObject.name; return true; }
            route = null;
            status = count == 0 ? "Waiting for one initialized scene command port." : "Multiple command ports are ready. Keep one active for testing.";
            return false;
        }

        private static async Task<CommandRouteOutcome> RouteAsync(string envelope, string remote, CancellationToken token)
        {
            if (!TryResolveLiveRoute(out var route, out var status))
                return new CommandRouteOutcome(null, CommandResult.Failure(CommandRoutingErrorCodes.RouteUnavailable, status), string.Empty);
            return await route.RouteMessageAsync(envelope, "editor-local", remote, token);
        }
    }
}
