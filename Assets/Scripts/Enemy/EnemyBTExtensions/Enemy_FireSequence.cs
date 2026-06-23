using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
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
                titleLabel = "Target",
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
}
