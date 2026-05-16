using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviourTree.Core
{
    public enum BlackBoardType
    {
        SELF = 0,
        SQUAD = 1,
    }

    public class BlackBoard : MonoBehaviour
    {
        [SerializeField] private List<UnityEngine.Object> serializedReferences = new();

        private BlackboardDefinition definition;

        private int[] intValues;
        private float[] floatValues;
        private bool[] boolValues;
        private Vector2[] vector2Values;
        private Vector3[] vector3Values;

        private GameObject[] gameObjectValues;
        private Transform[] transformValues;

        private int capacity;

        public BlackboardDefinition Definition => definition;

        public void Initialize(BlackboardDefinition blackboardDefinition)
        {
            if (definition == null || definition != blackboardDefinition)
            {
                definition = blackboardDefinition;
            }

            if (definition == null) return;

            int count = definition.sharedVariables.Count;
            capacity = count;

            intValues = new int[count];
            floatValues = new float[count];
            boolValues = new bool[count];
            vector2Values = new Vector2[count];
            vector3Values = new Vector3[count];
            gameObjectValues = new GameObject[count];
            transformValues = new Transform[count];

            while (serializedReferences.Count < count)
            {
                serializedReferences.Add(null);
            }

            for (int i = 0; i < count; i++)
            {
                Type type = FieldTypeHelper.GetSystemTypeFromName(definition.sharedVariables[i].typeName);
                object initVal = definition.sharedVariables[i].GetInitialValue();

                if (type == typeof(int))
                    intValues[i] = initVal != null ? (int)initVal : 0;
                else if (type == typeof(float))
                    floatValues[i] = initVal != null ? (float)initVal : 0f;
                else if (type == typeof(bool))
                    boolValues[i] = initVal != null ? (bool)initVal : false;
                else if (type == typeof(Vector2))
                    vector2Values[i] = initVal != null ? (Vector2)initVal : Vector2.zero;
                else if (type == typeof(Vector3))
                    vector3Values[i] = initVal != null ? (Vector3)initVal : Vector3.zero;
                else if (type == typeof(GameObject))
                    gameObjectValues[i] = serializedReferences[i] as GameObject;
                else if (type == typeof(Transform))
                    transformValues[i] = serializedReferences[i] as Transform;
            }
        }

        public void BuildSerializedReferences(BlackboardDefinition blackboardDefinition)
        {
            definition = blackboardDefinition;

            if (definition == null) return;

            int count = definition.sharedVariables.Count;
            while (serializedReferences.Count < count)
            {
                serializedReferences.Add(null);
            }
            while (serializedReferences.Count > count)
            {
                serializedReferences.RemoveAt(serializedReferences.Count - 1);
            }
        }

        public void ClearSerializedReferences()
        {
            definition = null;
            serializedReferences.Clear();
        }

        private bool OutOfRange(int index)
        {
            if (capacity == 0 || index < 0 || index >= capacity)
            {
                Debug.LogWarning($"[Blackboard] Invalid index {index}, capacity={capacity}");
                return true;
            }
            return false;
        }

        public int GetInt(int index) => OutOfRange(index) ? 0 : intValues[index];
        public void SetInt(int index, int value) { if (!OutOfRange(index)) intValues[index] = value; }

        public float GetFloat(int index) => OutOfRange(index) ? 0f : floatValues[index];
        public void SetFloat(int index, float value) { if (!OutOfRange(index)) floatValues[index] = value; }

        public bool GetBool(int index) => OutOfRange(index) ? false : boolValues[index];
        public void SetBool(int index, bool value) { if (!OutOfRange(index)) boolValues[index] = value; }

        public Vector2 GetVector2(int index) => OutOfRange(index) ? Vector2.zero : vector2Values[index];
        public void SetVector2(int index, Vector2 value) { if (!OutOfRange(index)) vector2Values[index] = value; }

        public Vector3 GetVector3(int index) => OutOfRange(index) ? Vector3.zero : vector3Values[index];
        public void SetVector3(int index, Vector3 value) { if (!OutOfRange(index)) vector3Values[index] = value; }

        public GameObject GetGameObject(int index) => OutOfRange(index) ? null : gameObjectValues[index];
        public void SetGameObject(int index, GameObject value)
        {
            if (OutOfRange(index)) return;
            gameObjectValues[index] = value;
            SyncSerializedRef(index, value);
        }

        public Transform GetTransform(int index) => OutOfRange(index) ? null : transformValues[index];
        public void SetTransform(int index, Transform value)
        {
            if (OutOfRange(index)) return;
            transformValues[index] = value;
            SyncSerializedRef(index, value);
        }

        private void SyncSerializedRef(int index, UnityEngine.Object unityObject)
        {
            if (definition != null && index < definition.sharedVariables.Count)
            {
                Type type = FieldTypeHelper.GetSystemTypeFromName(definition.sharedVariables[index].typeName);
                if (type != null && !type.IsValueType)
                {
                    if (index < serializedReferences.Count)
                    {
                        serializedReferences[index] = unityObject;
                    }
                }
            }
        }

        public T Get<T>(int index)
        {
            if (typeof(T) == typeof(int)) return (T)(object)GetInt(index);
            if (typeof(T) == typeof(float)) return (T)(object)GetFloat(index);
            if (typeof(T) == typeof(bool)) return (T)(object)GetBool(index);
            if (typeof(T) == typeof(Vector2)) return (T)(object)GetVector2(index);
            if (typeof(T) == typeof(Vector3)) return (T)(object)GetVector3(index);
                if (typeof(T) == typeof(GameObject)) return (T)(object)GetGameObject(index);
            if (typeof(T) == typeof(Transform)) return (T)(object)GetTransform(index);

            Debug.LogWarning($"[Blackboard] Unsupported type {typeof(T).Name}");
            return default;
        }

        public void Set<T>(int index, T value)
        {
            if (value is int vInt) { SetInt(index, vInt); return; }
            if (value is float vFloat) { SetFloat(index, vFloat); return; }
            if (value is bool vBool) { SetBool(index, vBool); return; }
            if (value is Vector2 v2) { SetVector2(index, v2); return; }
            if (value is Vector3 v3) { SetVector3(index, v3); return; }
            if (value is GameObject go) { SetGameObject(index, go); return; }
            if (value is Transform tr) { SetTransform(index, tr); return; }

            Debug.LogWarning($"[Blackboard] Unsupported type {typeof(T).Name}");
        }
    }
}
