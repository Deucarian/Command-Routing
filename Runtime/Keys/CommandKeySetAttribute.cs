using System;

namespace Deucarian.CommandRouting
{
    /// <summary>Marks an authoritative set of named CommandKey fields or properties for the Inspector.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CommandKeySetAttribute : Attribute { }
}
