using System;
using System.Collections.Generic;
using Deucarian.Diagnostics;
using Deucarian.Editor;
using UnityEditor;

namespace Deucarian.CommandRouting.Editor
{
    [InitializeOnLoad]
    internal static class CommandRoutingControlCenterRegistration
    {
        private const string PackageId = "com.deucarian.command-routing";
        private const string SettingsPath =
            "Assets/Deucarian/CommandRouting/CommandRoutingSettings.asset";
        private static readonly IDisposable ToolRegistration;
        private static readonly IDisposable CardRegistration;

        static CommandRoutingControlCenterRegistration()
        {
            ToolRegistration = DeucarianToolRegistry.Register(
                new DeucarianToolDescriptor(
                    DeucarianToolIds.CommandRouting,
                    "Command Routing",
                    "Configure and inspect transport-independent command routing.",
                    DeucarianControlCenterArea.Communication,
                    CommandRoutingEditorWindow.Open,
                    PackageId,
                    searchTerms: new[] { "command", "routing", "protocol", "tester" },
                    order: 100));

            CardRegistration = DeucarianControlCenterRegistry.RegisterCardProvider(
                new CommandRoutingCardProvider());
        }

        private sealed class CommandRoutingCardProvider :
            IDeucarianControlCenterCardProvider
        {
            public string Id => PackageId + ".control-center";

            public IEnumerable<DeucarianControlCenterCard> Capture(
                DeucarianControlCenterContext context)
            {
                CommandRoutingSettings settings =
                    AssetDatabase.LoadAssetAtPath<CommandRoutingSettings>(
                        SettingsPath);
                string validation =
                    CommandRoutingSettingsValidation.Validate(settings);
                bool configured = string.IsNullOrEmpty(validation);
                DiagnosticSummary diagnostics = CaptureDiagnostics(
                    "command-routing.");

                return new[]
                {
                    new DeucarianControlCenterCard(
                        PackageId + ".setup",
                        DeucarianControlCenterArea.Communication,
                        "Command Routing",
                        "Local configuration and sanitized runtime diagnostics.",
                        PackageId,
                        ResolveStatus(configured, diagnostics.Severity),
                        !configured
                            ? "Setup required"
                            : diagnostics.SectionCount == 0
                                ? "Configured; no live runtime"
                                : diagnostics.SectionCount +
                                  " live diagnostic section(s)",
                        order: 100,
                        details: new[]
                        {
                            configured
                                ? "Settings asset: configured"
                                : validation,
                            diagnostics.SectionCount == 0
                                ? "Live diagnostics: no active routing runtime"
                                : "Live diagnostics: " + diagnostics.Severity +
                                  " across " + diagnostics.SectionCount +
                                  " section(s)"
                        },
                        actions: new[]
                        {
                            new DeucarianControlCenterAction(
                                PackageId + ".open",
                                "Open Command Routing",
                                CommandRoutingEditorWindow.Open)
                        },
                        searchTerms: new[]
                        {
                            "command", "routing", "protocol", "diagnostics", "live"
                        })
                };
            }

            private static DiagnosticSummary CaptureDiagnostics(string prefix)
            {
                List<IDiagnosticProvider> providers =
                    new List<IDiagnosticProvider>();
                foreach (IDiagnosticProvider provider in
                    DiagnosticProviderRegistry.SnapshotProviders())
                {
                    if (provider != null &&
                        !string.IsNullOrEmpty(provider.ProviderId) &&
                        provider.ProviderId.StartsWith(
                            "command-routing.",
                            StringComparison.Ordinal))
                    {
                        providers.Add(provider);
                    }
                }

                DiagnosticReport report =
                    DiagnosticReportBuilder.BuildFrom(providers);
                int sectionCount = 0;
                DiagnosticSeverity severity = DiagnosticSeverity.Info;
                foreach (DiagnosticSection section in report.Sections)
                {
                    if (string.IsNullOrEmpty(section?.Id) ||
                        !section.Id.StartsWith(
                            prefix,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    sectionCount++;
                    if (section.Severity > severity)
                    {
                        severity = section.Severity;
                    }
                }

                return new DiagnosticSummary(sectionCount, severity);
            }

            private static DeucarianControlCenterStatus ResolveStatus(
                bool configured,
                DiagnosticSeverity severity)
            {
                if (severity == DiagnosticSeverity.Error)
                {
                    return DeucarianControlCenterStatus.Error;
                }

                if (!configured || severity == DiagnosticSeverity.Warning)
                {
                    return DeucarianControlCenterStatus.Warning;
                }

                return severity == DiagnosticSeverity.Success
                    ? DeucarianControlCenterStatus.Success
                    : DeucarianControlCenterStatus.Info;
            }
        }

        private readonly struct DiagnosticSummary
        {
            internal DiagnosticSummary(
                int sectionCount,
                DiagnosticSeverity severity)
            {
                SectionCount = sectionCount;
                Severity = severity;
            }

            internal int SectionCount { get; }
            internal DiagnosticSeverity Severity { get; }
        }
    }
}