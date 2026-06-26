using System;
using BehaviourTree.Core;
using UnityEngine;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Full fire pipeline: aim turret → search trajectory → fire delay → fire.
    /// Returns RUNNING during each phase, SUCCESS when complete, FAILURE on trajectory fail.
    /// All timing/damage values are ScriptableObjectConstant fields resolved from EnemyConfig at bake time.
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
            new DynamicParamDescriptor
            {
                titleLabel = "Fire Delay",
                label = "Fire Delay",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 1,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Spread",
                label = "Spread",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 2,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Reload",
                label = "Reload Duration",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 3,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Damage",
                label = "Damage",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 4,
                allowedTypes = new[] { typeof(int) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Trajectory Starting Height",
                label = "TrajectoryStartingHeight",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 5,
                allowedTypes = new[] { typeof(float) }
            },
        };

        private enum FirePhase { Aiming, Trajectory, FireDelay, Firing }

        private int targetSlot = -1;
        private float fireDelay;
        private float spread;
        private float reloadDuration;

        // projectileDamage is int in the node (from EnemyConfig) but GunHandling uses float
        private int projectileDamage;

        private FirePhase phase;
        private bool initialized;
        private GunHandling cachedGunHandling;
        private Transform target;
        private float fireDelayTimer;
        private float trajectoryStartingHeight = -1;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            int fieldIndex = 0;

            // Target (variable)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsVariable)
                targetSlot = fields[fieldIndex++].value;

            // Fire Delay (ScriptableObjectConstant → float)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                fireDelay = fields[fieldIndex++].GetFloat();

            // Spread (ScriptableObjectConstant → float)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                spread = fields[fieldIndex++].GetFloat();

            // Reload Duration (ScriptableObjectConstant → float)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                reloadDuration = fields[fieldIndex++].GetFloat();

            // Projectile Damage (ScriptableObjectConstant → int)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                projectileDamage = fields[fieldIndex++].GetInt();

            // Trajectory Starting Height (ScriptableObjectConstant → float)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                trajectoryStartingHeight = fields[fieldIndex].GetFloat();
        }

        protected override void OnInitialize()
        {
            cachedGunHandling = GetComponentFromBB<GunHandling>();
        }

        public override NodeState Execute(TickContext ctx)
        {
            if (targetSlot < 0 || cachedGunHandling == null) return NodeState.FAILURE;

            if (!initialized)
            {
                object targetObj = BB.GetBoxed(targetSlot);
                target = targetObj as Transform;
                if (target == null) return NodeState.FAILURE;

                // Push config values to GunHandling (only once per fire cycle)
                cachedGunHandling.SetSpread(spread);
                cachedGunHandling.SetDamage(projectileDamage);
                cachedGunHandling.SetReload(reloadDuration);
                cachedGunHandling.SetFireDelay(fireDelay);

                cachedGunHandling.SelectAimTarget(target);
                phase = FirePhase.Aiming;
                cachedGunHandling.SetAiming(true);
                initialized = true;
            }

            switch (phase)
            {
                case FirePhase.Aiming:
                    if (cachedGunHandling.OnTarget)
                        phase = FirePhase.Trajectory;
                    return NodeState.RUNNING;

                case FirePhase.Trajectory:
                {
                    TrajectorySearchState state = cachedGunHandling.SearchTrajectory(trajectoryStartingHeight);
                    if (state == TrajectorySearchState.Found)
                    {
                        fireDelayTimer = 0f;
                        phase = FirePhase.FireDelay;
                    }
                    else if (state == TrajectorySearchState.Failed)
                    {
                        Reset();
                        return NodeState.FAILURE;
                    }
                    return NodeState.RUNNING;
                }

                case FirePhase.FireDelay:
                    fireDelayTimer += Time.deltaTime;
                    if (fireDelayTimer >= fireDelay)
                    {
                        fireDelayTimer = 0f;
                        phase = FirePhase.Firing;
                    }
                    return NodeState.RUNNING;

                case FirePhase.Firing:
                    if (cachedGunHandling.IsReloading)
                        return NodeState.RUNNING;
                    cachedGunHandling.Fire();
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
            cachedGunHandling?.SetAiming(false);
            initialized = false;
            target = null;
            fireDelayTimer = 0f;
            phase = FirePhase.Aiming;
        }
    }
}
