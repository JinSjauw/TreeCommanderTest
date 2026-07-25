using BehaviourTree.Core;
using BehaviourTree.Runtime;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Single schema lookup for the inspector: a method's own
    /// GetDynamicParamDescriptors() override wins; otherwise its
    /// [SharedVar]/plain fields are projected into descriptors.
    /// </summary>
    public static class NodeParamSchema
    {
        static NodeParamSchema()
        {
            MethodRegistry.OnRegistryRebuilt += ParamSchemaReflection.Invalidate;
        }

        public static DynamicParamDescriptor[] GetForMethod(NodeMethod method)
        {
            if (method == null) return null;
            return method.GetDynamicParamDescriptors()
                   ?? ParamSchemaReflection.GetDescriptors(method.GetType());
        }
    }
}
