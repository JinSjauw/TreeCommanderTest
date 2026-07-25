using System;
using UnityEditor;
using UnityEngine;

namespace BehaviourTree.Editor
{
    /// <summary>
    /// GenericMenu-based picker for ScriptableObject config-source fields,
    /// used by ScriptableObjectConstant param rows. Extracted from CustomNodeEditor.
    /// </summary>
    public static class ConfigFieldPicker
    {
        /// <summary>
        /// Opens a GenericMenu listing fields from every SO in the tree's
        /// <see cref="BehaviourTreeAssetBase.availableConfigs"/> whose type
        /// is compatible with the expected parameter type.
        /// Selecting a field stores the SO reference (GUID + field name) as metadata;
        /// the actual value is resolved at bake time by TreeBaker.
        /// </summary>
        public static void ShowConfigFieldPicker(SerializedProperty entry, Type paramType,
            SerializedProperty configGuidProp, SerializedProperty configFieldProp,
            UnityEngine.Object dirtyTarget, Action onChanged)
        {
            var treeAsset = BehaviourTreeEditor.currentTree;
            if (treeAsset == null) return;

            GenericMenu menu = new GenericMenu();

            foreach (ScriptableObject so in treeAsset.availableConfigs)
            {
                if (so == null) continue;

                string soPath = AssetDatabase.GetAssetPath(so);
                string soGuid = AssetDatabase.AssetPathToGUID(soPath);
                if (string.IsNullOrEmpty(soGuid)) continue;

                System.Reflection.FieldInfo[] fields = so.GetType()
                    .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                System.Reflection.PropertyInfo[] properties = so.GetType()
                    .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                foreach (var f in fields)
                {
                    if (IsTypeCompatible(f.FieldType, paramType))
                    {
                        string itemPath = $"{so.name}/{f.Name}  ({f.FieldType.Name})";
                        string capturedGuid = soGuid;
                        string capturedField = f.Name;
                        ScriptableObject capturedSO = so;
                        menu.AddItem(new GUIContent(itemPath), false, () =>
                        {
                            ApplySOFieldSelection(entry, configGuidProp, configFieldProp,
                                capturedSO, capturedGuid, capturedField, dirtyTarget, onChanged);
                        });
                    }
                }

                foreach (var p in properties)
                {
                    if (p.CanRead && IsTypeCompatible(p.PropertyType, paramType))
                    {
                        string itemPath = $"{so.name}/{p.Name}  ({p.PropertyType.Name})";
                        string capturedGuid = soGuid;
                        string capturedField = p.Name;
                        ScriptableObject capturedSO = so;
                        menu.AddItem(new GUIContent(itemPath), false, () =>
                        {
                            ApplySOFieldSelection(entry, configGuidProp, configFieldProp,
                                capturedSO, capturedGuid, capturedField, dirtyTarget, onChanged);
                        });
                    }
                }
            }

            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No compatible fields in config sources"));

            menu.ShowAsContext();
        }

        /// <summary>
        /// Stores the SO reference metadata (GUID + field name) on the entry so the
        /// popup button can display which field is selected. The actual value is
        /// resolved at bake time by <see cref="TreeBaker.ResolveSOConstantEntry"/>
        /// which re-reads the current SO field value — so SO changes are always picked up.
        /// </summary>
        private static void ApplySOFieldSelection(SerializedProperty entry,
            SerializedProperty configGuidProp, SerializedProperty configFieldProp,
            ScriptableObject so, string guid, string fieldName,
            UnityEngine.Object dirtyTarget, Action onChanged)
        {
            SerializedProperty isConfig = entry.FindPropertyRelative("isConfigConstant");
            if (isConfig != null) isConfig.boolValue = true;
            configGuidProp.stringValue = guid;
            configFieldProp.stringValue = fieldName;

            entry.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(dirtyTarget);
            onChanged?.Invoke();
        }

        private static bool IsTypeCompatible(Type sourceType, Type targetType)
        {
            if (targetType == null) return true;
            return targetType.IsAssignableFrom(sourceType);
        }

        public static ScriptableObject GetConfigSourceByGuid(string guid)
        {
            var treeAsset = BehaviourTreeEditor.currentTree;
            if (treeAsset == null) return null;
            foreach (var so in treeAsset.availableConfigs)
            {
                if (so == null) continue;
                string soPath = AssetDatabase.GetAssetPath(so);
                string soGuid = AssetDatabase.AssetPathToGUID(soPath);
                if (soGuid == guid) return so;
            }
            return null;
        }
    }
}
