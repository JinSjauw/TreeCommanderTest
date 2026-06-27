using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Reduces a per-agent squadData array by computing the average, lowest, or highest
    /// value across all active agents and writing the result to an output variable.
    /// Works with int[], float[], Vector2[], and Vector3[] squadData arrays.
    /// Element count is determined by ctx.agentCount, which tracks currently registered agents.
    /// </summary>
    [NodeMethod("SquadReduce", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class SquadReduceMethod : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Source SquadData",
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
        private ReduceOperation operation;
        private int outputSlot = -1;
        private Type elementType;

        // ── Cached during first Execute ───────────────────────────────
        private Type runtimeType;
        private bool runtimeTypeResolved;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            int fieldIndex = 0;

            // Field 0: Source Array (Variable — stride marker is skipped, we use ctx.agentCount)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
            {
                sourceSlot = fields[fieldIndex].value;
                fieldIndex++;
                if (fieldIndex < fields.Length && fields[fieldIndex].IsStrideMarker)
                    fieldIndex++;
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
            int count = ctx.agentCount;
            Debug.Log($"[SquadReduce] sourceSlot={sourceSlot}, outputSlot={outputSlot}, agentCount={count}, operation={operation}, elementType={elementType}, runtimeType={runtimeType}");

            if (sourceSlot < 0 || outputSlot < 0 || count <= 0)
            {
                Debug.LogWarning($"[SquadReduce] FAILURE: invalid slots or zero agentCount (sourceSlot={sourceSlot}, outputSlot={outputSlot}, count={count})");
                return NodeState.FAILURE;
            }

            // ── Resolve runtime type once ─────────────────────────────
            if (!runtimeTypeResolved)
            {
                runtimeTypeResolved = true;
                if (elementType != null)
                {
                    runtimeType = elementType;
                    Debug.Log($"[SquadReduce] Resolved runtimeType from elementType: {runtimeType}");
                }
                else
                {
                    Debug.Log($"[SquadReduce] elementType is null, detecting from first non-null value...");
                    // Fallback: detect from first non-null value
                    for (int i = 0; i < count; i++)
                    {
                        object val = BB.GetBoxedRaw(sourceSlot + i);
                        Debug.Log($"[SquadReduce]   slot[{sourceSlot}+{i}] = {val ?? "null"} (type: {val?.GetType().Name ?? "null"})");
                        if (val != null)
                        {
                            runtimeType = val.GetType();
                            Debug.Log($"[SquadReduce]   → detected runtimeType: {runtimeType}");
                            break;
                        }
                    }
                }
            }

            if (runtimeType == null)
            {
                Debug.LogWarning($"[SquadReduce] FAILURE: runtimeType is null (no valid elements in source array)");
                return NodeState.FAILURE;
            }

            // ── Debug: dump all source values ──────────────────────────
            Debug.Log($"[SquadReduce] Reading {count} elements from slot {sourceSlot}:");
            for (int i = 0; i < count; i++)
            {
                object val = BB.GetBoxedRaw(sourceSlot + i);
                Debug.Log($"[SquadReduce]   [{i}] = {val ?? "null"} (type: {val?.GetType().Name ?? "null"}, nullVal: {IsNullValue(val)})");
            }

            // ── Reduce ────────────────────────────────────────────────
            object result = ComputeReduce(runtimeType, count, out bool success);
            if (!success)
            {
                Debug.LogWarning($"[SquadReduce] FAILURE: ComputeReduce returned failure for type={runtimeType}, operation={operation}");
                return NodeState.FAILURE;
            }

            Debug.Log($"[SquadReduce] SUCCESS: result={result}, writing to outputSlot={outputSlot}");
            BB.SetBoxed(outputSlot, result);
            return NodeState.SUCCESS;
        }

        private object ComputeReduce(Type type, int count, out bool success)
        {
            switch (operation)
            {
                case ReduceOperation.Average:
                    return ComputeAverage(type, count, out success);
                case ReduceOperation.Lowest:
                    return ComputeExtreme(type, count, highest: false, out success);
                case ReduceOperation.Highest:
                    return ComputeExtreme(type, count, highest: true, out success);
                default:
                    Debug.LogError($"[SquadReduce] ComputeReduce: unknown operation {operation}");
                    success = false;
                    return null;
            }
        }

        private object ComputeAverage(Type type, int count, out bool success)
        {
            if (type == typeof(int))
            {
                long sum = 0;
                int validCount = 0;
                for (int i = 0; i < count; i++)
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
                for (int i = 0; i < count; i++)
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
                for (int i = 0; i < count; i++)
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
                for (int i = 0; i < count; i++)
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
            
            Debug.LogError($"[SquadReduce] ComputeAverage: unsupported type {type}");
            success = false;
            return null;
        }

        private object ComputeExtreme(Type type, int count, bool highest, out bool success)
        {
            if (type == typeof(int))
            {
                int best = highest ? int.MinValue : int.MaxValue;
                bool found = false;
                for (int i = 0; i < count; i++)
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
                for (int i = 0; i < count; i++)
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
                for (int i = 0; i < count; i++)
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
                for (int i = 0; i < count; i++)
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

            Debug.LogError($"[SquadReduce] ComputeExtreme: unsupported type {type}");
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
