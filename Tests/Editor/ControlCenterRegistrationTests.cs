using System.Linq;
using Deucarian.Diagnostics;
using Deucarian.Editor;
using NUnit.Framework;

namespace Deucarian.CommandRouting.Tests
{
    public sealed class ControlCenterRegistrationTests
    {
        private const string PackageId =
            "com.deucarian.command-routing";

        [Test]
        public void PackageRegistersStableToolAndCard()
        {
            Assert.That(
                DeucarianToolRegistry.TryGet(
                    DeucarianToolIds.CommandRouting,
                    out DeucarianToolDescriptor tool),
                Is.True);
            Assert.That(tool.OwningPackage, Is.EqualTo(PackageId));

            DeucarianControlCenterSnapshot snapshot =
                DeucarianControlCenterSnapshotBuilder.Capture(true);
            Assert.That(
                snapshot.Cards.Any(
                    card => card.OwningPackage == PackageId),
                Is.True);
        }

        [Test]
        public void CardIncludesSanitizedRegisteredRuntimeSeverity()
        {
            using (DiagnosticProviderRegistration registration =
                   DiagnosticProviderRegistry.Register(
                       new ReviewDiagnosticProvider()))
            {
                DeucarianControlCenterCard card =
                    DeucarianControlCenterSnapshotBuilder.Capture(true)
                        .Cards.Single(candidate =>
                            candidate.Id == PackageId + ".setup");

                Assert.That(
                    card.Status,
                    Is.EqualTo(DeucarianControlCenterStatus.Error));
                Assert.That(
                    card.Details.Any(detail =>
                        detail.StartsWith("Live diagnostics:")),
                    Is.True);
                Assert.That(
                    string.Join(" ", card.Details),
                    Does.Not.Contain("raw-diagnostic-value"));
            }
        }

        private sealed class ReviewDiagnosticProvider : IDiagnosticProvider
        {
            public string ProviderId => "command-routing.review";
            public string DisplayName => "Review";

            public void Collect(DiagnosticReportBuilder builder)
            {
                builder.AddSection(ProviderId, DisplayName)
                    .AddItem(
                        "state",
                        "State",
                        "raw-diagnostic-value",
                        DiagnosticSeverity.Error);
            }
        }
    }
}