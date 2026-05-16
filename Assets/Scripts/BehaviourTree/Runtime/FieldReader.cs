using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree
{
    public ref struct FieldReader
    {
        private readonly ReadOnlySpan<FieldData> fields;
        private readonly BlackBoard blackboard;

        public FieldReader(ReadOnlySpan<FieldData> fields, BlackBoard blackboard)
        {
            this.fields = fields;
            this.blackboard = blackboard;
        }

        public int GetInt(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
            {
                return fd.GetInt();
            }

            return blackboard.GetInt(fd.value);
        }

        public void SetInt(int index, int value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
            {
                blackboard.SetInt(fd.value, value);
            }
            else
            {
                Debug.LogWarning($"[FieldReader.SetInt] Field {index} is constant — write ignored.");
            }
        }

        public float GetFloat(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
            {
                return fd.GetFloat();
            }

            return blackboard.GetFloat(fd.value);
        }

        public void SetFloat(int index, float value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
            {
                blackboard.SetFloat(fd.value, value);
            }
            else
            {
                Debug.LogWarning($"[FieldReader.SetFloat] Field {index} is constant — write ignored.");
            }
        }

        public bool GetBool(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
            {
                return fd.GetBool();
            }
            return blackboard.GetBool(fd.value);
        }

        public void SetBool(int index, bool value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
            {
                blackboard.SetBool(fd.value, value);
            }
            else
            {
                Debug.LogWarning($"[FieldReader.SetBool] Field {index} is constant — write ignored.");
            }
        }

        public Vector2 GetVector2(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
            {
                Debug.LogWarning($"[FieldReader.GetVector2] field is constant. Vector2 not supported");
                return Vector2.zero;
            }
            return blackboard.GetVector2(fd.value);
        }

        public void SetVector2(int index, Vector2 value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
            {
                blackboard.SetVector2(fd.value, value);
            }
            else
            {
                Debug.LogWarning($"[FieldReader.SetVector2] Field {index} is constant — write ignored.");
            }
        }

        public Vector3 GetVector3(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
            {
                Debug.LogWarning($"[FieldReader.GetVector3] field is constant. Vector3 not supported");
                return Vector3.zero;
            }
            return blackboard.GetVector3(fd.value);
        }

        public void SetVector3(int index, Vector3 value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
            {
                blackboard.SetVector3(fd.value, value);
            }
            else
            {
                Debug.LogWarning($"[FieldReader.SetVector3] Field {index} is constant — write ignored.");
            }
        }

        public GameObject GetGameObject(int index)
        {
            ref readonly FieldData fd = ref fields[index];

            if (fd.IsConstant)
            {
                Debug.LogWarning($"[FieldReader.GetGameObject] field is constant. GameObject not supported");
                return null;
            }

            return blackboard.GetGameObject(fd.value);
        }

        public void SetGameObject(int index, GameObject value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
            {
                blackboard.SetGameObject(fd.value, value);
            }
            else
            {
                Debug.LogWarning($"[FieldReader.SetGameObject] Field {index} is constant — write ignored.");
            }
        }

        public Transform GetTransform(int index)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsConstant)
            {
                Debug.LogWarning($"[FieldReader.GetTransform] field is constant. Transform not supported");
                return null;
            }

            return blackboard.GetTransform(fd.value);
        }

        public void SetTransform(int index, Transform value)
        {
            ref readonly FieldData fd = ref fields[index];
            if (fd.IsVariable)
            {
                blackboard.SetTransform(fd.value, value);
            }
            else
            {
                Debug.LogWarning($"[FieldReader.SetTransform] Field {index} is constant — write ignored.");
            }
        }
    }
}
