using System;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Opt-in marker for the binding accessor generator: public instance fields and
    /// properties of this Component type get generated typed push accessors for
    /// tracked bindings. Without it, tracked bindings use the Expression fallback.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GenerateBindingAccessorsAttribute : Attribute { }
}
