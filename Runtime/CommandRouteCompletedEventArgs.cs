using System;

namespace Deucarian.CommandRouting
{
    public sealed class CommandRouteCompletedEventArgs : EventArgs
    {
        public CommandRouteCompletedEventArgs(
            CommandRouteOutcome outcome,
            string effectiveTransport,
            string remoteEndpoint,
            double durationMilliseconds)
        {
            Outcome =
                outcome ??
                throw new ArgumentNullException(nameof(outcome));
            EffectiveTransport = Normalize(effectiveTransport);
            RemoteEndpoint = remoteEndpoint;
            DurationMilliseconds = BoundDuration(
                durationMilliseconds);
        }

        public CommandRouteOutcome Outcome { get; }

        public string EffectiveTransport { get; }

        /// <summary>
        /// Gets the effective endpoint associated with this route outcome.
        /// Decoded commands retain the package's command-metadata
        /// normalization; protocol rejections retain the route argument.
        /// </summary>
        public string RemoteEndpoint { get; }

        public double DurationMilliseconds { get; }

        internal CommandRouteCompletedEventArgs CreateSubscriberSnapshot()
        {
            return new CommandRouteCompletedEventArgs(
                CreateOutcomeSnapshot(Outcome),
                EffectiveTransport,
                RemoteEndpoint,
                DurationMilliseconds);
        }

        private static CommandRouteOutcome CreateOutcomeSnapshot(
            CommandRouteOutcome outcome)
        {
            CommandEnvelope command = outcome.Command;
            CommandEnvelope commandSnapshot = command == null
                ? null
                : new CommandEnvelope(
                    command.CommandName,
                    command.Payload,
                    command.CommandId,
                    command.ProtocolVersion,
                    new CommandMetadata(
                        command.Metadata?.Source,
                        command.Metadata?.Transport,
                        command.Metadata?.RemoteEndpoint),
                    command.RawEnvelope);
            CommandResult result = outcome.Result;
            CommandResult resultSnapshot = result.Succeeded
                ? CommandResult.Success(
                    result.Payload,
                    result.Message)
                : CommandResult.Failure(
                    result.ErrorCode,
                    result.Message,
                    result.Payload);
            return new CommandRouteOutcome(
                commandSnapshot,
                resultSnapshot,
                outcome.Response);
        }

        private static double BoundDuration(double value)
        {
            if (double.IsNaN(value) || value < 0d)
            {
                return 0d;
            }

            double maximum = TimeSpan.MaxValue.TotalMilliseconds;
            return double.IsInfinity(value) || value > maximum
                ? maximum
                : value;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
