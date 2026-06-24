using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Formation shape types.
    /// </summary>
    public enum FormationType
    {
        Circle = 0,
    }

    /// <summary>
    /// Commander node. Use inside ForEachRole/ForEachAgent to compute a per-agent
    /// formation position and write it to a squad-data Vector3 output variable.
    ///
    /// Parameters:
    ///   FormationCenter — center of the formation (C/V/SO toggle, Vector3)
    ///   Position Output — squad-data Vector3 variable to write to per agent
    ///   Formation Type  — enum dropdown (Circle)
    ///   Radius          — distance from center (ScriptableObjectConstant)
    ///   Agent Count     — auto-detected from Position Output stride
    /// </summary>
    [NodeMethod("CalculateFormation", allowedTreeType = AllowedTreeType.Commander)]
    public sealed class CalculateFormation : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Formation Center",
                label = "Center",
                kind = DynamicParamKind.Toggle,
                index = 0,
                allowedTypes = new[] { typeof(Vector3) },
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Position Output",
                label = "Output",
                kind = DynamicParamKind.Variable,
                index = 1,
                allowedTypes = new[] { typeof(Vector3[]) },
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Formation Type",
                label = "Type",
                kind = DynamicParamKind.Operation,
                index = 2,
                operationEnumType = typeof(FormationType),
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Radius",
                label = "Radius",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 3,
            },
        };

        // ── Deserialized state ────────────────────────────────────────

        private int centerSlot = -1;
        private Vector3 centerConstant;
        private bool hasCenterConstant;

        private int posSlot = -1;
        private int agentCount = 1;

        private FormationType formationType;

        private float radius;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            int fieldIndex = 0;

            // ── Field 0: FormationCenter (Toggle) ────────────────────
            if (fieldIndex < fields.Length)
            {
                if (fields[fieldIndex].IsVariable)
                {
                    centerSlot = VariableMethodHelper.ReadVariableSlot(fields, ref fieldIndex);
                }
                else
                {
                    hasCenterConstant = true;
                    object val = VariableMethodHelper.ReadConstant(fields, ref fieldIndex, fieldTypeNames, boxedConstants);
                    centerConstant = val is Vector3 v3 ? v3 : Vector3.zero;
                }
            }

            // ── Field 1: Position Output (Variable, squad-data array) ─
            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
            {
                posSlot = fields[fieldIndex].value;
                fieldIndex++;
                // TreeBaker emits a stride marker after squad-data variables
                if (fieldIndex < fields.Length && fields[fieldIndex].IsStrideMarker)
                {
                    agentCount = fields[fieldIndex].value;
                    Debug.Log($"CalculateFormation: agentCount: {agentCount}");
                    fieldIndex++;
                }
            }

            // ── Field 2: Formation Type (constant enum) ───────────────
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
            {
                formationType = (FormationType)fields[fieldIndex].value;
                fieldIndex++;
            }

            // ── Field 3: Radius (SO constant, baked as float) ────────
            if (fieldIndex < fields.Length)
            {
                object val = VariableMethodHelper.ReadConstant(fields, ref fieldIndex, fieldTypeNames, boxedConstants);
                radius = val is float f ? f : (val is int i ? (float)i : 5f);
            }
        }

        public override NodeState Execute()
        {
            if (posSlot < 0 || agentCount <= 0)
            {
                Debug.LogWarning($"[CalculateFormation] FAILED — posSlot={posSlot}, agentCount={agentCount}");
                return NodeState.FAILURE;
            }

            // Get current agent offset (set by ForEachRole/ForEachAgent)
            BlackBoard bb = BB as BlackBoard;
            int agentOffset = bb != null ? bb.currentAgentOffset : 0;

            // Read center (variable or constant)
            // centerSlot is a shared/commander-level variable — use GetBoxedRaw
            // to avoid applying currentAgentOffset (which is for per-agent squad data).
            Vector3 center = Vector3.zero;

            if(hasCenterConstant)
            {
                center = centerConstant;
            }
            else if(centerSlot >= 0)
            {
                object targetObj = BB.GetBoxedRaw(centerSlot);
                center = targetObj is Vector3 v3 ? v3 : Vector3.zero;
            }

            Vector3 position = center;

            if(agentOffset == 0)
            {
                BB.SetBoxed(posSlot, position);
                return NodeState.SUCCESS;
            }

            switch (formationType)
            {
                case FormationType.Circle:
                default:
                    float angle = (agentOffset / (float)(agentCount - 1)) * 360f * Mathf.Deg2Rad;
                    Debug.Log($"CalculateFormation: angle: {angle:F2} {agentCount}");
                    position = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                    break;
            }

            Debug.Log($"CalculateFormation: position: {position:F2} agentOffset={agentOffset}");
            // BB.SetBoxed internally adds currentAgentOffset, so pass posSlot directly.
            // Adding agentOffset manually would double-offset (posSlot + 2*agentOffset),
            BB.SetBoxed(posSlot, position);
            return NodeState.SUCCESS;
        }
    }
}
