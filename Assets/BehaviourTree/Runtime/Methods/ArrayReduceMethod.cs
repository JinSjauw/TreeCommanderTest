using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    public enum ReduceOperation
    {
        Average = 0,
        Lowest  = 1,
        Highest = 2,
    }

    /// <summary>
    /// Reduces a regular array variable by computing the average, lowest, or highest
    /// value and writing the result to an output variable.
    /// Works with int[], float[], Vector2[], and Vector3[] arrays.
    /// Element count is determined by the stride marker baked alongside the variable.
    /// </summary>
    [NodeMethod("ArrayReduce")]
    public sealed class ArrayReduceMethod : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Source Array",
                label = "Source",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(int[]), typeof(float[]), typeof(Vector2[]), typeof(Vector3[]) },
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Operation",
                label = "Op",
                kind = DynamicParamKind.Operation,
                index = 1,
                operationEnumType = typeof(ReduceOperation),
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Output",
                label = "Output",
                kind = DynamicParamKind.Variable,
                index = 2,
                syncTypeFromIndex = 0,
                syncElementType = true,
            },
        };

        // ── Deserialized state ────────────────────────────────────────

        private int sourceSlot = -1;
        private int elementCount = 1;
        private ReduceOperation operation;
        private int outputSlot = -1;
        private Type elementType;

        // ── Cached during first Execute to avoid per-frame lookups ────
        private Type runtimeType;
        private bool runtimeTypeResolved;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            int fieldIndex = 0;

            // Field 0: Source Array (Variable with stride marker)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
            {
                sourceSlot = fields[fieldIndex].value;
                fieldIndex++;

                if (fieldIndex < fields.Length && fields[fieldIndex].IsStrideMarker)
                {
                    elementCount = fields[fieldIndex].value;
                    fieldIndex++;
                }
            }

            // Field 1: Operation (constant enum)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
            {
                operation = (ReduceOperation)fields[fieldIndex].value;
                fieldIndex++;
            }

            // Field 2: Output (Variable)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
            {
                outputSlot = fields[fieldIndex].value;
                fieldIndex++;
                if (fieldIndex < fields.Length && fields[fieldIndex].IsStrideMarker)
                    fieldIndex++;
            }

            // Resolve element type from fieldTypeNames[0]
            // fieldTypeName may already be the element type (e.g. "Vector3")
            // or the array type (e.g. "Vector3[]"). Handle both.
            if (fieldTypeNames != null && fieldTypeNames.Length > 0)
            {
                string typeName = fieldTypeNames[0];
                if (!string.IsNullOrEmpty(typeName))
                {
                    elementType = FieldTypeHelper.TryGetSystemTypeFromName(typeName, out Type t) ? t : null;
                    if (elementType != null && elementType.IsArray)
                        elementType = elementType.GetElementType();
                }
            }
        }

        public override NodeState Execute(TickContext ctx)
        {
            Debug.Log($"[ArrayReduce] sourceSlot={sourceSlot}, outputSlot={outputSlot}, elementCount={elementCount}, operation={operation}, elementType={elementType}, runtimeType={runtimeType}");

            if (sourceSlot < 0 || outputSlot < 0 || elementCount <= 0)
            {
                Debug.LogWarning($"[ArrayReduce] FAILURE: invalid slots or zero elementCount (sourceSlot={sourceSlot}, outputSlot={outputSlot}, elementCount={elementCount})");
                return NodeState.FAILURE;
            }

            // ── Resolve runtime type once ─────────────────────────────
            if (!runtimeTypeResolved)
            {
                runtimeTypeResolved = true;
                if (elementType != null)
                {
                    runtimeType = elementType;
                    Debug.Log($"[ArrayReduce] Resolved runtimeType from elementType: {runtimeType}");
                }
                else
                {
                    Debug.Log($"[ArrayReduce] elementType is null, detecting from first non-null value...");
                    // Fallback: detect from first non-null value
                    for (int i = 0; i < elementCount; i++)
                    {
                        object val = BB.GetBoxedRaw(sourceSlot + i);
                        Debug.Log($"[ArrayReduce]   slot[{sourceSlot}+{i}] = {val ?? "null"} (type: {val?.GetType().Name ?? "null"})");
                        if (val != null)
                        {
                            runtimeType = val.GetType();
                            Debug.Log($"[ArrayReduce]   → detected runtimeType: {runtimeType}");
                            break;
                        }
                    }
                }
            }

            if (runtimeType == null)
            {
                Debug.LogWarning($"[ArrayReduce] FAILURE: runtimeType is null (no valid elements in source array)");
                return NodeState.FAILURE;
            }

            // ── Debug: dump all source values ──────────────────────────
            Debug.Log($"[ArrayReduce] Reading {elementCount} elements from slot {sourceSlot}:");
            for (int i = 0; i < elementCount; i++)
            {
                object val = BB.GetBoxedRaw(sourceSlot + i);
                Debug.Log($"[ArrayReduce]   [{i}] = {val ?? "null"} (type: {val?.GetType().Name ?? "null"}, nullVal: {IsNullValue(val)})");
            }

            // ── Reduce ────────────────────────────────────────────────
            object result = ComputeReduce(runtimeType, out bool success);
            if (!success)
            {
                Debug.LogWarning($"[ArrayReduce] FAILURE: ComputeReduce returned failure for type={runtimeType}, operation={operation}");
                return NodeState.FAILURE;
            }

            Debug.Log($"[ArrayReduce] SUCCESS: result={result}, writing to outputSlot={outputSlot}");
            BB.SetBoxed(outputSlot, result);
            return NodeState.SUCCESS;
        }

        private object ComputeReduce(Type type, out bool success)
        {
            success = true;

            switch (operation)
            {
                case ReduceOperation.Average:
                    return ComputeAverage(type, out success);
                case ReduceOperation.Lowest:
                    return ComputeExtreme(type, highest: false, out success);
                case ReduceOperation.Highest:
                    return ComputeExtreme(type, highest: true, out success);
                default:
                    success = false;
                    return null;
            }
        }

        private object ComputeAverage(Type type, out bool success)
        {
            if (type == typeof(int))
            {
                long sum = 0;
                int validCount = 0;
                for (int i = 0; i < elementCount; i++)
                {
                    object val = BB.GetBoxedRaw(sourceSlot + i);
                    if (val is int iv)
                    {
                        sum += iv;
                        validCount++;
                    }
                }
                success = validCount > 0;
                return success ? (object)(int)(sum / validCount) : null;
            }

            if (type == typeof(float))
            {
                float sum = 0f;
                int validCount = 0;
                for (int i = 0; i < elementCount; i++)
                {
                    object val = BB.GetBoxedRaw(sourceSlot + i);
                    if (val is float fv && !float.IsNaN(fv))
                    {
                        sum += fv;
                        validCount++;
                    }
                }
                success = validCount > 0;
                return success ? (object)(sum / validCount) : null;
            }

            if (type == typeof(Vector2))
            {
                Vector2 sum = Vector2.zero;
                int validCount = 0;
                for (int i = 0; i < elementCount; i++)
                {
                    object val = BB.GetBoxedRaw(sourceSlot + i);
                    if (val is Vector2 v)
                    {
                        sum += v;
                        validCount++;
                    }
                }
                success = validCount > 0;
                return success ? (object)(sum / validCount) : null;
            }

            if (type == typeof(Vector3))
            {
                Vector3 sum = Vector3.zero;
                int validCount = 0;
                for (int i = 0; i < elementCount; i++)
                {
                    object val = BB.GetBoxedRaw(sourceSlot + i);
                    if (val is Vector3 v)
                    {
                        sum += v;
                        validCount++;
                    }
                }
                success = validCount > 0;
                return success ? (object)(sum / validCount) : null;
            }

            success = false;
            return null;
        }

        private object ComputeExtreme(Type type, bool highest, out bool success)
        {
            if (type == typeof(int))
            {
                int best = highest ? int.MinValue : int.MaxValue;
                bool found = false;
                for (int i = 0; i < elementCount; i++)
                {
                    object val = BB.GetBoxedRaw(sourceSlot + i);
                    if (val is int iv)
                    {
                        found = true;
                        if (highest ? iv > best : iv < best)
                            best = iv;
                    }
                }
                success = found;
                return found ? (object)best : null;
            }

            if (type == typeof(float))
            {
                float best = highest ? float.MinValue : float.MaxValue;
                bool found = false;
                for (int i = 0; i < elementCount; i++)
                {
                    object val = BB.GetBoxedRaw(sourceSlot + i);
                    if (val is float fv && !float.IsNaN(fv))
                    {
                        found = true;
                        if (highest ? fv > best : fv < best)
                            best = fv;
                    }
                }
                success = found;
                return found ? (object)best : null;
            }

            if (type == typeof(Vector2))
            {
                Vector2 best = Vector2.zero;
                float bestSqr = highest ? float.MinValue : float.MaxValue;
                bool found = false;
                for (int i = 0; i < elementCount; i++)
                {
                    object val = BB.GetBoxedRaw(sourceSlot + i);
                    if (val is Vector2 v)
                    {
                        float sqr = v.sqrMagnitude;
                        if (!found || (highest ? sqr > bestSqr : sqr < bestSqr))
                        {
                            best = v;
                            bestSqr = sqr;
                            found = true;
                        }
                    }
                }
                success = found;
                return found ? (object)best : null;
            }

            if (type == typeof(Vector3))
            {
                Vector3 best = Vector3.zero;
                float bestSqr = highest ? float.MinValue : float.MaxValue;
                bool found = false;
                for (int i = 0; i < elementCount; i++)
                {
                    object val = BB.GetBoxedRaw(sourceSlot + i);
                    if (val is Vector3 v)
                    {
                        float sqr = v.sqrMagnitude;
                        if (!found || (highest ? sqr > bestSqr : sqr < bestSqr))
                        {
                            best = v;
                            bestSqr = sqr;
                            found = true;
                        }
                    }
                }
                success = found;
                return found ? (object)best : null;
            }

            success = false;
            return null;
        }

        private static bool IsNullValue(object value)
        {
            if (value == null) return true;
            if (value is int i && i == 0) return true;
            if (value is float f && Mathf.Approximately(f, 0f)) return true;
            if (value is Vector2 v2 && v2.sqrMagnitude < 0.0001f) return true;
            if (value is Vector3 v3 && v3.sqrMagnitude < 0.0001f) return true;
            return false;
        }
    }
}
