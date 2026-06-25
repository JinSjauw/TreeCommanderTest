using System;
using BehaviourTree.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Sets the NavMeshAgent.speed on this agent's GameObject.
    /// Speed can be a constant, a variable from the BB, or a ScriptableObject field.
    /// Caches the original speed and restores it when the subtree is aborted.
    /// </summary>
    [NodeMethod("SetNavAgentSpeed", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class SetNavAgentSpeed : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Move Speed",
                label = "Value",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 0,
                allowedTypes = new Type[] { typeof(float) }
            },
        };

        private int speedSlot = -1;
        private float speedConstant = 3.5f;
        private bool hasSpeedConstant;

        private NavMeshAgent cachedAgent;
        private float originalSpeed;
        private bool agentResolved;

        public override void DeserializeParameters(ReadOnlySpan<FieldData> fields, string[] fieldTypeNames, object[] boxedConstants)
        {
            if (fields.Length >= 1)
            {
                if (fields[0].IsVariable)
                {
                    int fieldIndex = 0;
                    speedSlot = VariableMethodHelper.ReadVariableSlot(fields, ref fieldIndex);
                }
                else
                {
                    hasSpeedConstant = true;
                    int fieldIndex = 0;
                    object val = VariableMethodHelper.ReadConstant(fields, ref fieldIndex, fieldTypeNames, boxedConstants);
                    speedConstant = val is float f ? f : (val is int i ? (float)i : 3.5f);
                }
            }
        }

        public override NodeState Execute(TickContext ctx)
        {
            if (!agentResolved)
            {
                cachedAgent = GetComponentFromBB<NavMeshAgent>();
                if (cachedAgent != null)
                    originalSpeed = cachedAgent.speed;
                agentResolved = true;
            }

            if (cachedAgent == null)
                return NodeState.FAILURE;

            float speed = hasSpeedConstant
                ? speedConstant
                : (speedSlot >= 0 ? Convert.ToSingle(BB.GetBoxed(speedSlot)) : originalSpeed);

            cachedAgent.speed = speed;
            return NodeState.SUCCESS;
        }

        public override void OnAbort(IBlackBoardAccess bbAccess)
        {
            if (cachedAgent != null)
                cachedAgent.speed = originalSpeed;
        }
    }
}
