using System;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEngine;

namespace BehaviourTree
{
    public class EnemyMethods
    {
        private static EnemyController GetController(BlackBoard blackBoard)
        {
            var ec = blackBoard.gameObject.GetComponent<EnemyController>();
            if (ec == null)
                Debug.LogError("[EnemyMethods] EnemyController not found on BlackBoard GameObject");
            return ec;
        }

        [BTreeMethod(MethodID.Enemy_MoveTo)]
        public static NodeState Enemy_MoveTo(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Enemy_MoveTo_NodeFields nodeFields = NodeFieldBindings.DeserializeEnemy_MoveTo(fields, blackBoard);
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            controller.Agent.SetDestination(nodeFields.TargetMovePosition);
            controller.Agent.isStopped = false;

            if (controller.HasArrivedAtDestination())
                return NodeState.SUCCESS;

            return NodeState.RUNNING;
        }

        [BTreeMethod(MethodID.Enemy_SelectPatrolPoint)]
        public static NodeState Enemy_SelectPatrolPoint(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Enemy_SelectPatrolPoint_NodeFields nodeFields = NodeFieldBindings.DeserializeEnemy_SelectPatrolPoint(fields, blackBoard);
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            Vector3 point = controller.SetNextPatrolPoint();
            nodeFields.TargetMovePosition = point;
            NodeFieldBindings.SerializeEnemy_SelectPatrolPoint(nodeFields, fields, blackBoard);
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.Enemy_SelectEngagePosition)]
        public static NodeState Enemy_SelectEngagePosition(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Enemy_SelectEngagePosition_NodeFields nodeFields = NodeFieldBindings.DeserializeEnemy_SelectEngagePosition(fields, blackBoard);
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;
            if (controller.AttackTarget == null) return NodeState.FAILURE;

            Vector3 position = controller.CalculateNewPathToTarget();
            nodeFields.TargetMovePosition = position;
            NodeFieldBindings.SerializeEnemy_SelectEngagePosition(nodeFields, fields, blackBoard);
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.Enemy_SelectAimTarget)]
        public static NodeState Enemy_SelectAimTarget(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;
            if (controller.AttackTarget == null) return NodeState.FAILURE;

            controller.SelectAimTarget();
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.Enemy_StartTrajectorySearch)]
        public static NodeState Enemy_StartTrajectorySearch(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            float startingHeight = controller.FireCurve.DesiredCurveHeight > 0f
                ? controller.FireCurve.DesiredCurveHeight
                : 1f;

            TrajectorySearchState searchState = controller.Trajectory.FindTrajectory(startingHeight);

            return searchState switch
            {
                TrajectorySearchState.Found => NodeState.SUCCESS,
                TrajectorySearchState.Failed => NodeState.FAILURE,
                _ => NodeState.RUNNING
            };
        }

        [BTreeMethod(MethodID.Enemy_WaitForReload)]
        public static NodeState Enemy_WaitForReload(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            controller.TickFiringCooldown(Time.deltaTime);

            if (controller.IsReloading)         
                return NodeState.RUNNING;

            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.Enemy_Fire)]
        public static NodeState Enemy_Fire(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            controller.Fire();
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.Enemy_StopMovement)]
        public static NodeState Enemy_StopMovement(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            controller.StopMoving();
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.Enemy_IsAimed)]
        public static NodeState Enemy_IsAimed(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            return controller.Aiming.OnTarget ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.Enemy_DetectTarget)]
        public static NodeState Enemy_DetectTarget(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            bool detected = controller.DetectTarget();
            return detected ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.Enemy_IsInFiringRange)]
        public static NodeState Enemy_IsInFiringRange(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            return controller.IsPlayerInFiringRadius() ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.Enemy_HasLineOfSight)]
        public static NodeState Enemy_HasLineOfSight(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            return controller.HasDirectLineOfSight() ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.Enemy_IsTrajectoryReady)]
        public static NodeState Enemy_IsTrajectoryReady(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            return controller.Trajectory.HasTrajectory ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.Enemy_HasArrived)]
        public static NodeState Enemy_HasArrived(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            return controller.HasArrivedAtDestination() ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }
}
