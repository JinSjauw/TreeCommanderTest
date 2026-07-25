using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Allocation-free typed access to blackboard storage. The implementation
    /// resolves the virtual slot through the layout map and reads the typed
    /// array directly. This is the hot-path API; GetBoxed/SetBoxed remain for
    /// tooling, JSON, and dynamic-type nodes.
    /// </summary>
    public interface IBlackboardTypedAccess
    {
        float GetFloat(int slot);
        void SetFloat(int slot, float value);
        int GetInt(int slot);
        void SetInt(int slot, int value);
        bool GetBool(int slot);
        void SetBool(int slot, bool value);
        Vector2 GetVector2(int slot);
        void SetVector2(int slot, Vector2 value);
        Vector3 GetVector3(int slot);
        void SetVector3(int slot, Vector3 value);
        Vector4 GetVector4(int slot);
        void SetVector4(int slot, Vector4 value);
        Color GetColor(int slot);
        void SetColor(int slot, Color value);
        Quaternion GetQuaternion(int slot);
        void SetQuaternion(int slot, Quaternion value);
        T GetObject<T>(int slot) where T : class;
        void SetObject(int slot, object value);
    }
}
