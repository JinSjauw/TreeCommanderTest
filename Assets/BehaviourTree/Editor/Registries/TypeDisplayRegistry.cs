using System;
using System.Collections.Generic;
using System.IO;
using BehaviourTree.Core;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// Per-type display metadata: human-readable name and colour used throughout
    /// the editor UI (inspector type labels, variable pickers, etc.).
    /// Access via <c>TypeDisplayRegistry.instance</c>. Auto-created at
    /// Assets/BehaviourTree/TypeDisplayRegistry.asset on domain reload if missing.
    /// </summary>
    [Serializable]
    public class TypeDisplayEntry
    {
        [Tooltip("Assembly-qualified type name used as the lookup key.")]
        public string typeName;

        [Tooltip("Human-readable name shown in the editor UI.")]
        public string displayName;

        [Tooltip("Colour used for the type label in rich-text labels.")]
        public Color displayColor = Color.white;
    }

    public sealed class TypeDisplayRegistry : ScriptableObject
    {
        private const string AssetPath = "Assets/BehaviourTree/TypeDisplayRegistry.asset";

        [SerializeField]
        private List<TypeDisplayEntry> entries = new();

        private Dictionary<Type, TypeDisplayEntry> lookup;

        private static TypeDisplayRegistry cachedInstance;

        public static TypeDisplayRegistry instance
        {
            get
            {
                if (cachedInstance == null)
                    cachedInstance = AssetDatabase.LoadAssetAtPath<TypeDisplayRegistry>(AssetPath);
                return cachedInstance;
            }
        }

        public static void InvalidateCache()
        {
            cachedInstance = null;
        }

        // ── Public API ─────────────────────────────────────────────

        /// <summary>Human-readable display name for a type.</summary>
        public string GetDisplayName(Type type)
        {
            if (type == null) return "Unknown";
            EnsureLookup();
            if (lookup.TryGetValue(type, out var entry) && !string.IsNullOrEmpty(entry.displayName))
                return entry.displayName;
            return type.Name;
        }

        /// <summary>Display colour for a type. Falls back to white if no entry exists.</summary>
        public Color GetDisplayColor(Type type)
        {
            if (type == null) return Color.white;
            EnsureLookup();
            if (lookup.TryGetValue(type, out var entry))
                return entry.displayColor;
            return Color.white;
        }

        /// <summary>HTML hex string (no #) suitable for rich-text &lt;color=#RRGGBB&gt; tags.</summary>
        public string GetRichColorHex(Type type)
        {
            Color c = GetDisplayColor(type);
            return ColorUtility.ToHtmlStringRGB(c);
        }

        // ── Internal ───────────────────────────────────────────────

        private void EnsureLookup()
        {
            if (lookup == null)
            {
                lookup = new Dictionary<Type, TypeDisplayEntry>();
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.typeName)) continue;
                    Type type = Type.GetType(entry.typeName);
                    if (type != null)
                        lookup[type] = entry;
                }
            }
        }

        private void OnEnable()
        {
            EnsureDefaults();
        }

        private void OnValidate()
        {
            lookup = null;
            InvalidateCache();
        }

        // ── Auto-creation ──────────────────────────────────────────

        [InitializeOnLoadMethod]
        private static void EnsureAssetExists()
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(AssetPath)))
                return;

            string dir = Path.GetDirectoryName(AssetPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string parent = Path.GetDirectoryName(dir);
                AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
            }

            TypeDisplayRegistry registry = CreateInstance<TypeDisplayRegistry>();
            AssetDatabase.CreateAsset(registry, AssetPath);
            AssetDatabase.SaveAssets();
            cachedInstance = null;
        }

        private void EnsureDefaults()
        {
            if (entries == null) entries = new List<TypeDisplayEntry>();
            if (entries.Count > 0) return;

            entries = new List<TypeDisplayEntry>
            {
                // ── Numeric ────────────────────────────────────────
                new TypeDisplayEntry
                {
                    typeName = typeof(int).AssemblyQualifiedName,
                    displayName = "Int",
                    displayColor = new Color(0.33f, 0.60f, 1.00f),  // blue
                },
                new TypeDisplayEntry
                {
                    typeName = typeof(float).AssemblyQualifiedName,
                    displayName = "Float",
                    displayColor = new Color(0.33f, 0.60f, 1.00f),  // blue
                },
                new TypeDisplayEntry
                {
                    typeName = typeof(double).AssemblyQualifiedName,
                    displayName = "Double",
                    displayColor = new Color(0.33f, 0.60f, 1.00f),  // blue
                },
                new TypeDisplayEntry
                {
                    typeName = typeof(uint).AssemblyQualifiedName,
                    displayName = "UInt",
                    displayColor = new Color(0.33f, 0.60f, 1.00f),  // blue
                },

                // ── Boolean ────────────────────────────────────────
                new TypeDisplayEntry
                {
                    typeName = typeof(bool).AssemblyQualifiedName,
                    displayName = "Bool",
                    displayColor = new Color(0.33f, 0.80f, 0.33f),  // green
                },

                // ── String ─────────────────────────────────────────
                new TypeDisplayEntry
                {
                    typeName = typeof(string).AssemblyQualifiedName,
                    displayName = "String",
                    displayColor = new Color(0.87f, 0.87f, 0.33f),  // yellow
                },

                // ── Vectors ────────────────────────────────────────
                new TypeDisplayEntry
                {
                    typeName = typeof(Vector2).AssemblyQualifiedName,
                    displayName = "Vector2",
                    displayColor = new Color(0.33f, 0.80f, 0.80f),  // cyan
                },
                new TypeDisplayEntry
                {
                    typeName = typeof(Vector3).AssemblyQualifiedName,
                    displayName = "Vector3",
                    displayColor = new Color(0.33f, 0.80f, 0.80f),  // cyan
                },
                new TypeDisplayEntry
                {
                    typeName = typeof(Vector4).AssemblyQualifiedName,
                    displayName = "Vector4",
                    displayColor = new Color(0.33f, 0.80f, 0.80f),  // cyan
                },

                // ── Special ────────────────────────────────────────
                new TypeDisplayEntry
                {
                    typeName = typeof(Color).AssemblyQualifiedName,
                    displayName = "Color",
                    displayColor = new Color(0.80f, 0.33f, 0.80f),  // magenta
                },
                new TypeDisplayEntry
                {
                    typeName = typeof(Quaternion).AssemblyQualifiedName,
                    displayName = "Quaternion",
                    displayColor = new Color(0.87f, 0.60f, 0.27f),  // orange
                },

                // ── Reference types ────────────────────────────────
                new TypeDisplayEntry
                {
                    typeName = typeof(GameObject).AssemblyQualifiedName,
                    displayName = "GameObject",
                    displayColor = new Color(0.70f, 0.70f, 0.70f),  // light gray
                },
                new TypeDisplayEntry
                {
                    typeName = typeof(Transform).AssemblyQualifiedName,
                    displayName = "Transform",
                    displayColor = new Color(0.70f, 0.70f, 0.70f),  // light gray
                },
                new TypeDisplayEntry
                {
                    typeName = typeof(Material).AssemblyQualifiedName,
                    displayName = "Material",
                    displayColor = new Color(0.70f, 0.70f, 0.70f),  // light gray
                },
            };

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
