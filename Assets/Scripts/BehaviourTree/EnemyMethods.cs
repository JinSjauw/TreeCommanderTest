using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime
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
        
        #region Pathfinding

        [BTreeMethod(MethodID.Enemy_MoveTo)]
        public static NodeState Enemy_MoveTo(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Enemy_MoveTo_NodeFields nodeFields = NodeFieldBindings.DeserializeEnemy_MoveTo(fields, blackBoard);
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            controller.Agent.SetDestination(nodeFields.targetMovePosition);
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
            if (nodeFields.patrolPointsParent == null) return NodeState.FAILURE;

            Vector3 point = nodeFields.selectionMode switch
            {
                PatrolPointSelection.Random => controller.SetRandomPatrolPoint(nodeFields.patrolPointsParent),
                PatrolPointSelection.Sequential => controller.SetNextPatrolPoint(nodeFields.patrolPointsParent),
                _ => controller.SetRandomPatrolPoint(nodeFields.patrolPointsParent)
            };

            nodeFields.targetMovePosition = point;
            controller.Agent.SetDestination(point);
            NodeFieldBindings.SerializeEnemy_SelectPatrolPoint(nodeFields, fields, blackBoard);
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.Enemy_SelectEngagePosition)]
        public static NodeState Enemy_SelectEngagePosition(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Enemy_SelectEngagePosition_NodeFields nodeFields = NodeFieldBindings.DeserializeEnemy_SelectEngagePosition(fields, blackBoard);
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            Vector3 position = controller.CalculateNewPathToTarget();
            nodeFields.targetMovePosition = position;
            NodeFieldBindings.SerializeEnemy_SelectEngagePosition(nodeFields, fields, blackBoard);
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.Enemy_MoveTo_Transform)]
        public static NodeState Enemy_MoveTo_Transform(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Enemy_MoveTo_Transform_NodeFields nodeFields = NodeFieldBindings.DeserializeEnemy_MoveTo_Transform(fields, blackBoard);
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;
            if (nodeFields.target == null) return NodeState.FAILURE;

            controller.Agent.SetDestination(nodeFields.target.position);
            controller.Agent.isStopped = false;

            if (controller.HasArrivedAtDestination())
                return NodeState.SUCCESS;

            return NodeState.RUNNING;
        }

        [BTreeMethod(MethodID.Enemy_HasArrived)]
        public static NodeState Enemy_HasArrived(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            return controller.HasArrivedAtDestination() ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.Enemy_StopMovement)]
        public static NodeState Enemy_StopMovement(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            controller.StopMoving();
            return NodeState.SUCCESS;
        }

        #endregion

        #region Aiming

        [BTreeMethod(MethodID.Enemy_SelectAimTarget)]
        public static NodeState Enemy_SelectAimTarget(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;
            
            bool success = controller.SelectAimTarget();
            
            if(!success) return NodeState.FAILURE;

            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.Enemy_StartTrajectorySearch)]
        public static NodeState Enemy_StartTrajectorySearch(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            TrajectorySearchState searchState = controller.SearchTrajectory();

            NodeState result;

            switch (searchState)
            {
                case TrajectorySearchState.Found:
                    result = NodeState.SUCCESS;
                    break;
                case TrajectorySearchState.Failed:
                    result = NodeState.FAILURE;
                    break;
                default:
                    result = NodeState.RUNNING;
                    break;
            }

            return result;
        }

        [BTreeMethod(MethodID.Enemy_IsTrajectoryReady)]
        public static NodeState Enemy_IsTrajectoryReady(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            return controller.GunHandling.HasTrajectory ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.Enemy_SetAiming)]
        public static NodeState Enemy_SetAiming(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            Enemy_SetAiming_NodeFields nodeFields = NodeFieldBindings.DeserializeEnemy_SetAiming(fields, blackBoard);
            controller.GunHandling.SetAiming(nodeFields.aiming);
            return NodeState.SUCCESS;
        }

        #endregion
        
        #region Firing

        [BTreeMethod(MethodID.Enemy_FireSequence)]
        public static NodeState Enemy_FireSequence(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Enemy_FireSequence_NodeFields nodeFields = NodeFieldBindings.DeserializeEnemy_FireSequence(fields, blackBoard);
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            Transform target = nodeFields.selectedTarget;
            if (target == null) return NodeState.FAILURE;

            GunHandling gunHandling = controller.GunHandling;
            gunHandling.SetFiringCooldown(nodeFields.reloadDuration);
            gunHandling.SetFireDelay(nodeFields.firingDelay);

            if (gunHandling.IsReloading) return NodeState.RUNNING;

            // Phase: Select aim target
            if (!gunHandling.HasAimTarget && !gunHandling.HasFailed)
            {
                gunHandling.SelectAimTarget(target);
                gunHandling.SetAiming(true);
                return NodeState.RUNNING;
            }

            // Phase: Search trajectory
            if (gunHandling.HasAimTarget && !gunHandling.HasTrajectory && !gunHandling.HasFailed)
            {
                TrajectorySearchState searchState = gunHandling.SearchTrajectory();
                switch (searchState)
                {
                    case TrajectorySearchState.Found:
                        break;
                    case TrajectorySearchState.Searching:
                        return NodeState.RUNNING;
                    case TrajectorySearchState.Failed:
                        gunHandling.Trajectory.ResetTrajectory();
                        return NodeState.FAILURE;
                }
            }

            // Phase: Wait for turret to aim
            if (gunHandling.HasTrajectory && !gunHandling.OnTarget)
            {
                return NodeState.RUNNING;
            }

            // Phase: Wait for firing delay
            if (!gunHandling.TickFiringDelay(Time.deltaTime))
            {
                return NodeState.RUNNING;
            }

            // Phase: Fire
            if (gunHandling.HasTrajectory && gunHandling.OnTarget)
            {
                gunHandling.Fire();
                return NodeState.SUCCESS;
            }

            return NodeState.FAILURE;
        }
        
        [BTreeMethod(MethodID.Enemy_WaitForReload)]
        public static NodeState Enemy_WaitForReload(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            if (controller.GunHandling.IsReloading)         
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

        [BTreeMethod(MethodID.Enemy_IsAimed)]
        public static NodeState Enemy_IsAimed(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            return controller.GunHandling.OnTarget ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        #endregion
        
        #region Detection
        
        [BTreeMethod(MethodID.Enemy_DetectTarget)]
        public static NodeState Enemy_DetectTarget(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            bool detected = controller.Detection.DetectTargets();

            return detected ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.Enemy_SelectDetectedTarget)]
        public static NodeState Enemy_SelectDetectedTarget(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Enemy_SelectDetectedTarget_NodeFields nodeFields = NodeFieldBindings.DeserializeEnemy_SelectDetectedTarget(fields, blackBoard);
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;

            Transform selected = controller.SelectTarget(nodeFields.strategy);
            if (selected == null)
                return NodeState.FAILURE;

            nodeFields.selectedTarget = selected;
            NodeFieldBindings.SerializeEnemy_SelectDetectedTarget(nodeFields, fields, blackBoard);
            return NodeState.SUCCESS;
        }

        [BTreeMethod(MethodID.Enemy_IsInFiringRange)]
        public static NodeState Enemy_IsInFiringRange(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Enemy_IsInFiringRange_NodeFields nodeFields = NodeFieldBindings.DeserializeEnemy_IsInFiringRange(fields, blackBoard);
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;
            
            return controller.TargetInFiringRange(nodeFields.selectedTarget) ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        [BTreeMethod(MethodID.Enemy_HasLineOfSight)]
        public static NodeState Enemy_HasLineOfSight(BlackBoard blackBoard, ReadOnlySpan<FieldData> fields)
        {
            Enemy_HasLineOfSight_NodeFields nodeFields = NodeFieldBindings.DeserializeEnemy_HasLineOfSight(fields, blackBoard);
            EnemyController controller = GetController(blackBoard);
            if (controller == null) return NodeState.FAILURE;
            return controller.HasLineOfSightToTarget(nodeFields.selectedTarget) ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        #endregion
    }
}
