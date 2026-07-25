using System;
using System.Reflection;
using UnityEngine;

namespace BehaviourTree.Core
{
    /// <summary>
    /// Maps a field/member Type to the typed IBlackboardTypedAccess accessor
    /// for that type. Used when compiling read/write delegates so they hit
    /// allocation-free typed accessors instead of generic Get&lt;T&gt;/Set&lt;T&gt;.
    ///
    /// MUST agree with BlackboardStorageLayout.Classify: a type maps to a typed
    /// accessor here iff Classify puts it in the matching typed array.
    /// </summary>
    public static class TypedAccessorMap
    {
        public static bool IsIntBackedEnum(Type t) =>
            t != null && t.IsEnum && Enum.GetUnderlyingType(t) == typeof(int);

        public static MethodInfo GetGetter(Type t)
        {
            if (t == typeof(float)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetFloat));
            if (t == typeof(bool)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetBool));
            if (t == typeof(int) || IsIntBackedEnum(t)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetInt));
            if (t == typeof(Vector2)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetVector2));
            if (t == typeof(Vector3)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetVector3));
            if (t == typeof(Vector4)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetVector4));
            if (t == typeof(Color)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetColor));
            if (t == typeof(Quaternion)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.GetQuaternion));
            return null;
        }

        public static MethodInfo GetSetter(Type t)
        {
            if (t == typeof(float)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetFloat));
            if (t == typeof(bool)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetBool));
            if (t == typeof(int) || IsIntBackedEnum(t)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetInt));
            if (t == typeof(Vector2)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetVector2));
            if (t == typeof(Vector3)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetVector3));
            if (t == typeof(Vector4)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetVector4));
            if (t == typeof(Color)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetColor));
            if (t == typeof(Quaternion)) return typeof(IBlackboardTypedAccess).GetMethod(nameof(IBlackboardTypedAccess.SetQuaternion));
            return null;
        }
    }
}
