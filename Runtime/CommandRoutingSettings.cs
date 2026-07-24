using UnityEngine;

namespace Deucarian.CommandRouting
{
    public sealed class CommandRoutingSettings : ScriptableObject
    {
        public const int DefaultHistoryCapacity = 100;
        public const int DefaultMaximumMessageCharacters = 65536;

        [SerializeField, Min(1)]
        private int historyCapacity = DefaultHistoryCapacity;

        [SerializeField, Min(256)]
        private int maximumMessageCharacters =
            DefaultMaximumMessageCharacters;

        [SerializeField]
        private bool logSuccessfulCommands = true;

        [SerializeField]
        private bool logFailedCommands = true;

        public int HistoryCapacity =>
            Mathf.Max(1, historyCapacity);

        public int MaximumMessageCharacters =>
            Mathf.Max(256, maximumMessageCharacters);

        public bool LogSuccessfulCommands =>
            logSuccessfulCommands;

        public bool LogFailedCommands =>
            logFailedCommands;
    }
}
