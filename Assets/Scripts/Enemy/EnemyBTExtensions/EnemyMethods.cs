using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    public enum RangeCheckOp
    {
        LessThan,
        GreaterThan
    }

    public enum PatrolMode
    {
        Sequential,
        Random
    }

    /// <summary>
    /// Checks whether the NavMeshAgent has reached its current destination.
    /// Returns SUCCESS when arrived, FAILURE while still moving.
    /// </summary>
    [NodeMethod("Enemy_HasArrived", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_HasArrived : ConditionMethod
    {
        private EnemyController cachedController;
        private bool controllerResolved;

        public override NodeState Execute()
        {
            if (!controllerResolved)
            {
                BlackBoard bb = BB as BlackBoard;
                if (bb != null)
                    cachedController = bb.GetComponent<EnemyController>();
                controllerResolved = true;
            }
            if (cachedController == null) return NodeState.FAILURE;

            return cachedController.HasArrivedAtDestination()
                ? NodeState.SUCCESS
                : NodeState.FAILURE;
        }
    }

    /// <summary>
    /// Runs target detection and selects one target by strategy.
    /// Writes the selected Transform to a blackboard variable.
    /// Returns SUCCESS if a target was found and selected, FAILURE if none.
    /// </summary>
    [NodeMethod("Enemy_SelectDetectedTarget", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_SelectDetectedTarget : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                label = "Target",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(Transform) }
            },
            new DynamicParamDescriptor
            {
                label = "Strategy",
                kind = DynamicParamKind.Operation,
                index = 1,
                operationEnumType = typeof(SelectionStrategy)
            },
        };

        private int targetSlot = -1;
        private SelectionStrategy strategy;
        private EnemyController cachedController;
        private bool controllerResolved;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            int fieldIndex = 0;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
                targetSlot = fields[fieldIndex++].value;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                strategy = (SelectionStrategy)fields[fieldIndex].value;
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0) return NodeState.FAILURE;

            if (!controllerResolved)
            {
                BlackBoard bb = BB as BlackBoard;
                if (bb != null)
                    cachedController = bb.GetComponent<EnemyController>();
                controllerResolved = true;
            }
            if (cachedController == null) return NodeState.FAILURE;

            Transform selected = cachedController.SelectTarget(strategy);
            if (selected == null) return NodeState.FAILURE;

            BB.SetBoxed(targetSlot, selected);
            return NodeState.SUCCESS;
        }
    }

    /// <summary>
    /// Full fire pipeline: aim turret → search trajectory → fire.
    /// Returns RUNNING during each phase, SUCCESS when the full cycle completes,
    /// FAILURE if the trajectory search fails.
    /// State resets on SUCCESS, FAILURE, or abort.
    /// </summary>
    [NodeMethod("Enemy_FireSequence", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_FireSequence : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                label = "Target",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(Transform) }
            },
        };

        private enum FirePhase { Aiming, Trajectory, Firing }

        private int targetSlot = -1;
        private FirePhase phase;
        private bool initialized;
        private GunHandling gunHandling;
        private Transform target;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                targetSlot = fields[0].value;
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0) return NodeState.FAILURE;

            if (!initialized)
            {
                object targetObj = BB.GetBoxed(targetSlot);
                target = targetObj as Transform;
                if (target == null) return NodeState.FAILURE;

                BlackBoard bb = BB as BlackBoard;
                if (bb != null)
                {
                    gunHandling = bb.GetComponent<GunHandling>();
                    if (gunHandling == null)
                        gunHandling = bb.GetComponentInChildren<GunHandling>();
                }
                if (gunHandling == null) return NodeState.FAILURE;

                gunHandling.SelectAimTarget(target);
                phase = FirePhase.Aiming;
                gunHandling.SetAiming(true);
                initialized = true;
            }

            switch (phase)
            {
                case FirePhase.Aiming:
                    if (gunHandling.OnTarget)
                        phase = FirePhase.Trajectory;
                    return NodeState.RUNNING;

                case FirePhase.Trajectory:
                {
                    TrajectorySearchState state = gunHandling.SearchTrajectory();
                    if (state == TrajectorySearchState.Found)
                    {
                        phase = FirePhase.Firing;
                    }
                    else if (state == TrajectorySearchState.Failed)
                    {
                        Reset();
                        return NodeState.FAILURE;
                    }
                    return NodeState.RUNNING;
                }

                case FirePhase.Firing:
                    if (gunHandling.IsReloading)
                        return NodeState.RUNNING;
                    gunHandling.Fire();
                    Reset();
                    return NodeState.SUCCESS;

                default:
                    return NodeState.FAILURE;
            }
        }

        public override void OnAbort(IBlackBoardAccess bbAccess)
        {
            Reset();
        }

        private void Reset()
        {
            gunHandling.SetAiming(false);
            initialized = false;
            target = null;
            gunHandling = null;
            phase = FirePhase.Aiming;
        }
    }

    /// <summary>
    /// Selects the next patrol point from the children of a Transform parent.
    /// Writes the chosen Vector3 position to a blackboard variable.
    /// Supports Sequential and Random modes.
    /// </summary>
    [NodeMethod("Enemy_SelectPatrolPoint", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_SelectPatrolPoint : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                label = "Patrol Points Parent",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(Transform) }
            },
            new DynamicParamDescriptor
            {
                label = "Output Position",
                kind = DynamicParamKind.Variable,
                index = 1,
                allowedTypes = new[] { typeof(Vector3) }
            },
            new DynamicParamDescriptor
            {
                label = "Mode",
                kind = DynamicParamKind.Operation,
                index = 2,
                operationEnumType = typeof(PatrolMode)
            },
        };

        private int parentSlot = -1;
        private int outputSlot = -1;
        private PatrolMode mode;
        private EnemyController cachedController;
        private bool controllerResolved;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            int fieldIndex = 0;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
                parentSlot = fields[fieldIndex++].value;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
                outputSlot = fields[fieldIndex++].value;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                mode = (PatrolMode)fields[fieldIndex].value;
        }

        public override NodeState Execute()
        {
            if (parentSlot < 0 || outputSlot < 0) return NodeState.FAILURE;

            if (!controllerResolved)
            {
                BlackBoard bb = BB as BlackBoard;
                if (bb != null)
                    cachedController = bb.GetComponent<EnemyController>();
                controllerResolved = true;
            }
            if (cachedController == null) return NodeState.FAILURE;

            object parentObj = BB.GetBoxed(parentSlot);
            Transform parent = parentObj as Transform;
            if (parent == null) return NodeState.FAILURE;

            Vector3 position = mode == PatrolMode.Random
                ? cachedController.SetRandomPatrolPoint(parent)
                : cachedController.SetNextPatrolPoint(parent);

            BB.SetBoxed(outputSlot, position);
            return NodeState.SUCCESS;
        }
    }

    /// <summary>
    /// Checks whether the 2D distance between the enemy's NavMeshAgent and a target
    /// Transform satisfies the given comparison against a radius.
    /// Radius can be a constant float or a blackboard float variable (C/V toggle).
    /// </summary>
    [NodeMethod("Enemy_CheckRange", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_CheckRange : ConditionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                label = "Target",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(Transform) }
            },
            new DynamicParamDescriptor
            {
                label = "Radius",
                kind = DynamicParamKind.Toggle,
                index = 1,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                label = "Operation",
                kind = DynamicParamKind.Operation,
                index = 2,
                operationEnumType = typeof(RangeCheckOp)
            },
        };

        private int targetSlot = -1;
        private int radiusSlot = -1;
        private float constantRadius;
        private bool hasConstantRadius;
        private RangeCheckOp operation;
        private EnemyController cachedController;
        private bool controllerResolved;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            int fieldIndex = 0;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
                targetSlot = fields[fieldIndex++].value;

            if (fieldIndex < fields.Length)
            {
                if (fields[fieldIndex].IsVariable)
                {
                    radiusSlot = fields[fieldIndex++].value;
                }
                else
                {
                    hasConstantRadius = true;
                    constantRadius = fields[fieldIndex].GetFloat();
                    fieldIndex++;
                }
            }

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                operation = (RangeCheckOp)fields[fieldIndex].value;
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0) return NodeState.FAILURE;

            if (!controllerResolved)
            {
                BlackBoard bb = BB as BlackBoard;
                if (bb != null)
                    cachedController = bb.GetComponent<EnemyController>();
                controllerResolved = true;
            }
            if (cachedController == null) return NodeState.FAILURE;

            object targetObj = BB.GetBoxed(targetSlot);
            Transform target = targetObj as Transform;
            if (target == null) return NodeState.FAILURE;

            float radius;
            if (radiusSlot >= 0)
            {
                object radiusObj = BB.GetBoxed(radiusSlot);
                radius = radiusObj is float f ? f : 0f;
            }
            else if (hasConstantRadius)
            {
                radius = constantRadius;
            }
            else
            {
                return NodeState.FAILURE;
            }

            float distance = Vector2.Distance(
                new Vector2(cachedController.Agent.transform.position.x, cachedController.Agent.transform.position.z),
                new Vector2(target.position.x, target.position.z));

            bool result = operation switch
            {
                RangeCheckOp.LessThan    => distance < radius,
                RangeCheckOp.GreaterThan => distance > radius,
                _ => false
            };
            return result ? NodeState.SUCCESS : NodeState.FAILURE;
        }
    }

    /// <summary>
    /// Checks whether there is a clear line of sight (no obstacles) between
    /// the enemy's eye transform and the target Transform.
    /// Uses Physics.Linecast against the Ground layer.
    /// </summary>
    [NodeMethod("Enemy_HasLineOfSight", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_HasLineOfSight : ConditionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                label = "Target",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(Transform) }
            },
        };

        private int targetSlot = -1;
        private EnemyController cachedController;
        private bool controllerResolved;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            if (fields.Length >= 1 && fields[0].IsVariable)
                targetSlot = fields[0].value;
        }

        public override NodeState Execute()
        {
            if (targetSlot < 0) return NodeState.FAILURE;

            if (!controllerResolved)
            {
                BlackBoard bb = BB as BlackBoard;
                if (bb != null)
                    cachedController = bb.GetComponent<EnemyController>();
                controllerResolved = true;
            }
            if (cachedController == null) return NodeState.FAILURE;

            object targetObj = BB.GetBoxed(targetSlot);
            Transform target = targetObj as Transform;
            if (target == null) return NodeState.FAILURE;

            return cachedController.HasLineOfSightToTarget(target)
                ? NodeState.SUCCESS
                : NodeState.FAILURE;
        }
    }

    /// <summary>
    /// Runs the enemy's internal detection system and checks whether any detected
    /// target satisfies the range check (and optionally line-of-sight).
    /// Returns SUCCESS if at least one target passes all active checks.
    /// </summary>
    [NodeMethod("Enemy_Detected", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_Detected : ConditionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                label = "Radius",
                kind = DynamicParamKind.Constant,
                index = 0,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                label = "Operation",
                kind = DynamicParamKind.Operation,
                index = 1,
                operationEnumType = typeof(RangeCheckOp)
            },
            new DynamicParamDescriptor
            {
                label = "Check LOS",
                kind = DynamicParamKind.Constant,
                index = 2,
                allowedTypes = new[] { typeof(bool) }
            },
        };

        private float radius;
        private RangeCheckOp operation;
        private bool checkLineOfSight;
        private EnemyController cachedController;
        private bool controllerResolved;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            int fieldIndex = 0;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                radius = fields[fieldIndex++].GetFloat();

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                operation = (RangeCheckOp)fields[fieldIndex++].value;

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                checkLineOfSight = fields[fieldIndex].GetBool();
        }

        public override NodeState Execute()
        {
            if (!controllerResolved)
            {
                BlackBoard bb = BB as BlackBoard;
                if (bb != null)
                    cachedController = bb.GetComponent<EnemyController>();
                controllerResolved = true;
            }
            if (cachedController == null) return NodeState.FAILURE;

            EnemyDetectionSystem detection = cachedController.Detection;
            if (detection == null) return NodeState.FAILURE;

            if (!detection.DetectTargets())
                return NodeState.FAILURE;

            Vector2 agentPos = new Vector2(
                cachedController.Agent.transform.position.x,
                cachedController.Agent.transform.position.z);

            foreach (Transform target in detection.DetectedTargets)
            {
                float distance = Vector2.Distance(
                    agentPos,
                    new Vector2(target.position.x, target.position.z));

                bool inRange = operation switch
                {
                    RangeCheckOp.LessThan    => distance < radius,
                    RangeCheckOp.GreaterThan => distance > radius,
                    _ => false
                };

                if (!inRange) continue;

                if (checkLineOfSight && !cachedController.HasLineOfSightToTarget(target))
                    continue;

                return NodeState.SUCCESS;
            }

            return NodeState.FAILURE;
        }
    }

    /// <summary>
    /// Stops the NavMeshAgent immediately (sets isStopped = true).
    /// </summary>
    [NodeMethod("Enemy_StopMovement", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_StopMovement : ActionMethod
    {
        private EnemyController cachedController;
        private bool controllerResolved;

        public override NodeState Execute()
        {
            if (!controllerResolved)
            {
                BlackBoard bb = BB as BlackBoard;
                if (bb != null)
                    cachedController = bb.GetComponent<EnemyController>();
                controllerResolved = true;
            }
            if (cachedController == null) return NodeState.FAILURE;

            cachedController.StopMoving();
            return NodeState.SUCCESS;
        }
    }
}
