using System;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Union struct that holds either a constant value or a blackboard variable name.
    /// Stored per-field on ActionNode.
    /// </summary>
    [Serializable]
    public struct NodeFieldEntry
    {
        /// <summary>Name matching the field in the *_Params struct.</summary>
        public string fieldName;

        /// <summary>Whether this entry points to a blackboard variable.</summary>
        public bool isVariable;
        
        /// <summary>Whether this entry points to an toggle variable.</summary>
        public bool isToggleVariable;

        /// <summary>Whether this entry points to an array variable.</summary>
        public bool isArray;

        /// <summary>Blackboard variable name (only used when isVariable == true).</summary>
        public string variableName;

        /// <summary>Assembly-qualified name of the System.Type for this field.
        /// Populated by the tree editor when creating field entries.</summary>
        public string fieldTypeName;

        /// <summary>
        /// Order name string for IsOrderDropdown fields. Stored alongside the
        /// resolved index (intValue) so the baker can survive OrderRegistry reordering.
        /// Also usable for future string-based constant types.
        /// </summary>
        public string stringValue;

        /// <summary>
        /// When true, the baker resolves the order name from stringValue against
        /// the OrderRegistry rather than using intValue directly. Set by the editor
        /// for fields marked [SharedVar(IsOrderDropdown = true)].
        /// </summary>
        public bool isOrderConstant;

        /// <summary>
        /// When non-empty, this entry sources its constant value from a field on a
        /// ScriptableObject registered in the tree's config sources list.
        /// Only metadata (GUID + field name) is stored here; the actual value is
        /// resolved at bake time by TreeBaker.ResolveSOConstantEntry so SO changes
        /// are always picked up.
        /// </summary>
        public string configSourceGuid;

        /// <summary>Field name on the ScriptableObject referenced by configSourceGuid.</summary>
        public string configFieldName;

        /// <summary>
        /// True when the user has toggled to SO constant mode (C/V/SO = SO),
        /// even if no specific field has been selected yet.
        /// Mutually exclusive with <see cref="isVariable"/>.
        /// </summary>
        public bool isConfigConstant;

        // Constant values (only one used, determined by fieldTypeName)
        public int intValue;
        public float floatValue;
        public bool boolValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public GameObject gameObjectValue;
        public Transform transformValue;
    }
}
