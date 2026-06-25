using System;
using BehaviourTree.Core;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Computes a random position within a tunable cone directed toward the target
    /// and writes it to a Vector3 blackboard variable. Pair with MoveTo to move there.
    ///
    /// Cone tuning (via ScriptableObjectConstant):
    ///   Cone Angle  - half-angle in degrees. Small (~15-30) = aggressive narrow cone,
    ///                 large (~90-180) = defensive near-circular spread.
    ///   Min Dist    - minimum random offset from current position.
    ///   Max Dist    - maximum random offset from current position.
    ///   Maintain Dist - ideal distance from the target; affects how "pushed out"
    ///                   the engage position is when already close to the target.
    ///
    /// Flank behavior is a separate future node.
    /// </summary>
    [NodeMethod("Enemy_EngagePosition", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_EngagePosition : ActionMethod
    {
        public override DynamicParamDescriptor[] GetDynamicParamDescriptors() => new[]
        {
            new DynamicParamDescriptor
            {
                titleLabel = "Position",
                label = "Output Position",
                kind = DynamicParamKind.Variable,
                index = 0,
                allowedTypes = new[] { typeof(Vector3) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Target",
                label = "Target",
                kind = DynamicParamKind.Variable,
                index = 1,
                allowedTypes = new[] { typeof(Transform) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Cone Angle",
                label = "Cone Half Angle",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 2,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Min Dist",
                label = "Min Distance",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 3,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Max Dist",
                label = "Max Distance",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 4,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Maintain Dist",
                label = "Maintain Distance",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 5,
                allowedTypes = new[] { typeof(float) }
            },
        };

        private Transform cachedOrigin;
        private int outputSlot = -1;
        private int targetSlot = -1;
        private float coneAngle;
        private float minDist;
        private float maxDist;
        private float maintainDistance;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            int fieldIndex = 0;

            outputSlot = ReadVariableSlot(fields, ref fieldIndex);
            targetSlot = ReadVariableSlot(fields, ref fieldIndex);

            // Cone Angle (ScriptableObjectConstant -> float)
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                coneAngle = fields[fieldIndex++].GetFloat();

            // Min Distance
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                minDist = fields[fieldIndex++].GetFloat();

            // Max Distance
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                maxDist = fields[fieldIndex++].GetFloat();

            // Maintain Distance
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                maintainDistance = fields[fieldIndex].GetFloat();
        }

        protected override void OnInitialize()
        {
            NavMeshAgent agent = GetComponentFromBB<NavMeshAgent>();
            cachedOrigin = agent?.transform;
        }

        public override NodeState Execute(TickContext ctx)
        {
            if (outputSlot < 0 || targetSlot < 0)
            {
                Debug.LogError($"Enemy_EngagePosition: Output or target slot not set. {outputSlot} {targetSlot}");
                return NodeState.FAILURE;
            }

            // Resolve target position
            object targetObj = BB.GetBoxed(targetSlot);
            Vector3 targetPos;
            if (targetObj is Transform t)
                targetPos = t.position;
            else if (targetObj is Vector3 v3)
                targetPos = v3;
            else if (targetObj is GameObject g)
                targetPos = g.transform.position;
            else
            {
                Debug.LogError("Enemy_EngagePosition: Target is not a Transform, GameObject or Vector3.");
                return NodeState.FAILURE;
            }

            if (cachedOrigin == null)
            {
                Debug.LogError("Enemy_EngagePosition: NavMeshAgent not found on BB.");
                return NodeState.FAILURE;
            }

            Vector3 origin = cachedOrigin.position;
            Vector3 engagePos = CalculateEngagePosition(origin, targetPos);

            //Debug.Log($"[Enemy_EngagePosition] Execute engagePos={engagePos} (origin={origin} target={targetPos}) coneAngle={coneAngle} minDist={minDist} maxDist={maxDist} maintainDistance={maintainDistance}");

            BB.SetBoxed(outputSlot, engagePos);
            return NodeState.SUCCESS;
        }

        private Vector3 CalculateEngagePosition(Vector3 origin, Vector3 target)
        {
            Vector3 directionToTarget = (target - origin).normalized;
            float distanceToTarget = Vector3.Distance(target, origin);

            // Distance-to-cone modulation:
            // Close → wide cone + short move + backwards-facing cone (defensive retreat).
            // Far  → narrow cone + long move + forward-facing cone (aggressive push).
            float distRatio = distanceToTarget / maintainDistance;
            float coneScale, clampedAlpha, dirBlend;
            if (distRatio < 1f)
            {
                float t = 1f - distRatio;                     // 1 at 0, 0 at maintainDist
                coneScale = Mathf.Lerp(1f, 3f, t);            // 3× cone at 0
                clampedAlpha = Mathf.Lerp(0.1f, 0.05f, t);   // tiny move at 0 → 0.1 at maintainDist
                dirBlend = 1f - t;                             // forward at maintainDist → backward at 0
            }
            else
            {
                float t = Mathf.Clamp01(distRatio - 1f);      // 0 at maintainDist, 1 at 2×
                coneScale = Mathf.Lerp(1f, 0.3f, t);          // narrow cone at range
                clampedAlpha = Mathf.Lerp(0.1f, 1f, t);       // long move at range
                dirBlend = 1f;                                 // always forward beyond maintainDist
            }

            // Lerp base direction from backward (at distance 0) to forward (at maintainDist+)
            Vector3 baseDir = Vector3.Lerp(-directionToTarget, directionToTarget, dirBlend);

            Quaternion randomRotation = Quaternion.Euler(
                Random.Range(-coneAngle * coneScale, coneAngle * coneScale),
                Random.Range(-coneAngle * coneScale, coneAngle * coneScale),
                0f);
            Vector3 randomDirection = randomRotation * baseDir;
            float randomDistance = Random.Range(
                minDist * clampedAlpha,
                maxDist * clampedAlpha);

            return origin + randomDirection.normalized * randomDistance;
        }
    }
}
