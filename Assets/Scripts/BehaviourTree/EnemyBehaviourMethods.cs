using System;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEngine;
using UnityEngine.Scripting;

namespace BehaviourTree
{
    public class EnemyBehaviourMethods
    {
        private static EnemyController GetController(BlackBoard blackBoard)
        {
            return blackBoard.GetComponent<EnemyController>();
        }

        [Preserve]
        [BTreeMethod(MethodID.AIM)]
        public static NodeState Aim(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);

            AIM_Params p = new AIM_Params();
            var reader = new FieldReader(fields, blackBoard);
            p.startingHeight = reader.GetFloat(0);
            controller.StopMoving();

            TrajectorySearchState result = controller.Trajectory.FindTrajectory(p.startingHeight);

            return result switch
            {
                TrajectorySearchState.Found => NodeState.SUCCESS,
                TrajectorySearchState.Failed => NodeState.FAILURE,
                _ => NodeState.RUNNING
            };
        }

        [Preserve]
        [BTreeMethod(MethodID.SELECT_AIM_TARGET)]
        public static NodeState SelectAimTarget(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            controller.SelectAimTarget();
            return NodeState.SUCCESS;
        }

        [Preserve]
        [BTreeMethod(MethodID.HAS_AIM_TARGET)]
        public static NodeState HasAimTarget(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            return controller.HasAimTarget ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [Preserve]
        [BTreeMethod(MethodID.ATTACK)]
        public static NodeState Attack(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            controller.Fire();
            return NodeState.SUCCESS;
        }

        [Preserve]
        [BTreeMethod(MethodID.SET_NEXT_PATROL_POINT)]
        public static NodeState SetNextPatrolPoint(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            controller.SetNextPatrolPoint();
            return NodeState.SUCCESS;
        }

        [Preserve]
        [BTreeMethod(MethodID.SET_NEXT_ATTACK_POINT)]
        public static NodeState SetNextAttackPoint(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            controller.CalculateNewPathToTarget();
            return NodeState.SUCCESS;
        }

        [Preserve]
        [BTreeMethod(MethodID.TARGET_IN_RANGE)]
        public static NodeState TargetInRange(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            return controller.IsPlayerInFiringRadius() ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [Preserve]
        [BTreeMethod(MethodID.HAS_TRAJECTORY)]
        public static NodeState HasTrajectory(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            return controller.Trajectory.HasTrajectory ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [Preserve]
        [BTreeMethod(MethodID.IS_ON_TARGET)]
        public static NodeState IsOnTarget(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            return controller.Aiming.OnTarget ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [Preserve]
        [BTreeMethod(MethodID.IS_RELOADING)]
        public static NodeState IsReloading(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            return controller.IsReloading ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [Preserve]
        [BTreeMethod(MethodID.HAS_DIRECT_LOS)]
        public static NodeState HasDirectLos(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            return controller.HasDirectLineOfSight() ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [Preserve]
        [BTreeMethod(MethodID.DETECT_TARGET)]
        public static NodeState DetectTarget(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            return controller.DetectTarget() ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [Preserve]
        [BTreeMethod(MethodID.HAS_TARGET)]
        public static NodeState HasTarget(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            return controller.AttackTarget != null ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [Preserve]
        [BTreeMethod(MethodID.ARRIVED_AT_DESTINATION)]
        public static NodeState ArrivedAtDestination(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var controller = GetController(blackBoard);
            return controller.HasArrivedAtDestination() ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [Preserve]
        [BTreeMethod(MethodID.MOVE_TO)]
        public static NodeState MoveTo(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            var agent = GetController(blackBoard).Agent;
            if (agent == null)
                return NodeState.FAILURE;

            MOVE_TO_Params p = new MOVE_TO_Params();
            var reader = new FieldReader(fields, blackBoard);
            p.target = reader.GetTransform(0);
            p.arrivalDistance = reader.GetFloat(1);

            if (p.target == null)
                return NodeState.FAILURE;

            agent.isStopped = false;
            agent.SetDestination(p.target.position);

            if (agent.remainingDistance <= p.arrivalDistance && !agent.pathPending)
                return NodeState.SUCCESS;

            return NodeState.RUNNING;
        }
    }
}
