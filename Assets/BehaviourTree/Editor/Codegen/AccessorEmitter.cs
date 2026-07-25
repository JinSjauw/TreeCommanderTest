using System;
using System.Reflection;
using System.Text;
using BehaviourTree.Core;

namespace BehaviourTree.EditorTools.Codegen
{
    /// <summary>
    /// Emits C# registration code for one (method type, field) binding accessor.
    /// Typed-vs-generic classification goes through TypedAccessorMap at generation
    /// time, so it cannot drift from the runtime layout.
    /// </summary>
    public static class AccessorEmitter
    {
        /// <summary>
        /// Returns one GeneratedAccessorRegistry.Register(...) statement for the field,
        /// or null when the field needs no accessor (skipAutoResolve).
        /// </summary>
        public static string EmitRegistration(Type methodType, FieldInfo field)
        {
            if (methodType == null || field == null) return null;

            var sharedVar = field.GetCustomAttribute<SharedVarAttribute>();
            if (sharedVar != null && sharedVar.SkipAutoResolve) return null;

            Type fieldType = field.FieldType;
            string cast = $"(({FormatTypeName(methodType)})m).{field.Name}";
            string fieldTypeName = FormatTypeName(fieldType);

            MethodInfo getter = TypedAccessorMap.GetGetter(fieldType);
            MethodInfo setter = TypedAccessorMap.GetSetter(fieldType);

            string readBody;
            string writeBody;

            if (getter != null && fieldType.IsEnum)
            {
                // int-backed enum: read as int, cast back; write cast to int.
                readBody = $"{cast} = ({fieldTypeName})bb.{getter.Name}(slot)";
                writeBody = $"bb.{setter.Name}(slot, (int){cast})";
            }
            else if (getter != null)
            {
                readBody = $"{cast} = bb.{getter.Name}(slot)";
                writeBody = $"bb.{setter.Name}(slot, {cast})";
            }
            else
            {
                readBody = $"{cast} = bb.Get<{fieldTypeName}>(slot)";
                writeBody = $"bb.Set(slot, {cast})";
            }

            var sb = new StringBuilder(256);
            sb.Append("            R(typeof(").Append(FormatTypeName(methodType)).Append("), \"")
              .Append(field.Name).Append("\", typeof(").Append(fieldTypeName).Append("),\n");
            sb.Append("                (m, bb, slot) => ").Append(readBody).Append(",\n");
            sb.Append("                (m, bb, slot) => ").Append(writeBody).Append(");\n");
            return sb.ToString();
        }

        /// <summary>
        /// Emits a TrackedAccessorRegistry registration for one component member
        /// (field or property with getter). Returns null for member types without a
        /// typed accessor (those stay on the Expression fallback by design).
        /// </summary>
        public static string EmitTrackedRegistration(Type compType, MemberInfo member)
        {
            Type memberType = member is FieldInfo f ? f.FieldType
                : member is PropertyInfo p ? p.PropertyType
                : null;
            if (memberType == null) return null;

            MethodInfo setter = TypedAccessorMap.GetSetter(memberType);
            if (setter == null) return null; // boxed fallback covers object/custom types

            string compTypeName = FormatTypeName(compType);
            string read = $"(({compTypeName})c).{member.Name}";
            string body = memberType.IsEnum
                ? $"bb.{setter.Name}(slot, (int){read})"
                : $"bb.{setter.Name}(slot, {read})";

            return $"            TR(typeof({compTypeName}), \"{member.Name}\", typeof({FormatTypeName(memberType)}),\n" +
                   $"                (c, bb, slot) => {body});\n";
        }

        /// <summary>Full-name type formatting valid in any namespace context (global::).</summary>
        public static string FormatTypeName(Type t)
        {
            if (t == null) return "object";
            if (t == typeof(float)) return "float";
            if (t == typeof(int)) return "int";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(string)) return "string";
            if (t == typeof(object)) return "object";
            if (t == typeof(void)) return "void";
            return "global::" + t.FullName.Replace('+', '.');
        }
    }
}
