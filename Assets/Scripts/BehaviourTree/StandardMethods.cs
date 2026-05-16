using System;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Scripting;

namespace BehaviourTree
{
    public class StandardMethods
    {
        [BTreeDecoratorMethod(MethodID.INVERTER)]
        public static NodeState Inverter(NodeState childResult, BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            INVERTER_Params p = new INVERTER_Params();
            var reader = new FieldReader(fields, blackBoard);
            p.alwaysFailure = reader.GetBool(0);
            p.alwaysSuccess = reader.GetBool(1);

            if (p.alwaysFailure) return NodeState.FAILURE;
            if (p.alwaysSuccess) return NodeState.SUCCESS;

            return childResult switch
            {
                NodeState.SUCCESS => NodeState.FAILURE,
                NodeState.FAILURE => NodeState.SUCCESS,
                _ => childResult
            };
        }

        [BTreeDecoratorMethod(MethodID.REPEATER)]
        public static NodeState Repeater(NodeState childResult, BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            if (childResult == NodeState.RUNNING)
                return NodeState.RUNNING;

            REPEATER_Params p = new REPEATER_Params();
            var reader = new FieldReader(fields, blackBoard);
            p.targetCount = reader.GetInt(0);
            p.currentCount = reader.GetInt(1);

            if (childResult == NodeState.SUCCESS && p.currentCount < p.targetCount)
            {
                p.currentCount++;
                reader.SetInt(1, p.currentCount);
                return NodeState.RUNNING;
            }

            reader.SetInt(1, 0);
            return childResult;
        }

        [Preserve]
        [BTreeMethod(MethodID.CHECK_FLAG)]
        public static NodeState CheckFlag(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            CHECK_FLAG_Params p = new CHECK_FLAG_Params();
            var reader = new FieldReader(fields, blackBoard);
            p.flagToCheck = reader.GetBool(0);

            bool value = p.flagToCheck;
            return value ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [Preserve]
        [BTreeMethod(MethodID.SET_FLAG)]
        public static NodeState SetFlag(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            SET_FLAG_Params p = new SET_FLAG_Params();
            var reader = new FieldReader(fields, blackBoard);
            p.valueToSet = reader.GetBool(0);
            p.flagToSet = reader.GetBool(1);

            //blackBoard.Set(p.flagToSet, p.valueToSet);
            return NodeState.SUCCESS;
        }

        [Preserve]
        [BTreeMethod(MethodID.COMPARE_FLOAT)]
        public static NodeState CompareFloat(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            COMPARE_FLOAT_Params p = new COMPARE_FLOAT_Params();
            var reader = new FieldReader(fields, blackBoard);
            p.value = reader.GetFloat(0);
            p.threshold = reader.GetFloat(1);
            p.operation = (CompareFloatOperation)reader.GetInt(2);

            //Create enum for operation;
            bool result = p.operation switch
            {
                CompareFloatOperation.LessThan => p.value < p.threshold,
                CompareFloatOperation.GreaterThan => p.value > p.threshold,
                CompareFloatOperation.Equal => p.value == p.threshold,
                CompareFloatOperation.NotEqual => p.value != p.threshold,
                CompareFloatOperation.LessThanOrEqual => p.value >= p.threshold,
                CompareFloatOperation.GreaterThanOrEqual => Mathf.Abs(p.value - p.threshold) < 0.001f,
                _ => false
            };

            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [Preserve]
        [BTreeMethod(MethodID.TICK_COOLDOWN)]
        public static NodeState TickCooldown(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            TICK_COOLDOWN_Params p = new TICK_COOLDOWN_Params();
            var reader = new FieldReader(fields, blackBoard);
            p.delay = reader.GetFloat(0);
            p.cooldownToTick = reader.GetFloat(1);

            p.cooldownToTick += Time.deltaTime;

            if (p.cooldownToTick >= p.delay)
            {
                reader.SetFloat(1, 0f);
                return NodeState.SUCCESS;
            }

            reader.SetFloat(1, p.cooldownToTick);
            return NodeState.RUNNING;
        }

        [Preserve]
        [BTreeMethod(MethodID.WAIT)]
        public static NodeState Wait(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            WAIT_Params p = new WAIT_Params();
            var reader = new FieldReader(fields, blackBoard);
            p.waitTime = reader.GetFloat(0);
            p.timer = reader.GetFloat(1);

            p.timer += Time.deltaTime;

            if (p.timer >= p.waitTime)
            {
                reader.SetFloat(1, 0f);
                return NodeState.SUCCESS;
            }

            reader.SetFloat(1, p.timer);
            return NodeState.RUNNING;
        }
    }

}
