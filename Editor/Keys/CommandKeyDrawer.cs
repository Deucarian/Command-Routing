using System;
using Deucarian.Editor;
using UnityEditor;

namespace Deucarian.CommandRouting.Editor
{
    [CustomPropertyDrawer(typeof(CommandKey<>), true)]
    public sealed class CommandKeyDrawer : DeucarianKeyDrawer
    {
        public override Type KeyType => typeof(CommandKey<>);
        public override Type DefinitionSetAttribute => typeof(CommandKeySetAttribute);
        public override string SetupHint => "Select an existing CommandKey; declare reusable keys once in a [CommandKeySet] class.";
    }
}
