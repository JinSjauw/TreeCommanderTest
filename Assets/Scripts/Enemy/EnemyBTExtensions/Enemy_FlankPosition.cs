using System;
using BehaviourTree.Core;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace BehaviourTree.Runtime.Methods
{
    /// <summary>
    /// Computes a random flanking position perpendicular to the target direction,
    /// writes it to a Vector3 blackboard variable. Pair with MoveTo to move there.
    ///
    /// Tuning (via ScriptableObjectConstant):
    ///   Flank Angle - base rotation from perpendicular toward the target.
    ///                 e.g. 60° = aggressive wrap, 90° = pure side flank.
    ///   Cone Angle  - random spread half-angle around the base direction.
    ///   Min Dist    - minimum random offset from current position.
    ///   Max Dist    - maximum random offset from current position.
    ///   Maintain Dist - ideal distance from target; when closer, offset shrinks.
    /// </summary>
    [NodeMethod("Enemy_FlankPosition", allowedTreeType = AllowedTreeType.Agent)]
    public sealed class Enemy_FlankPosition : ActionMethod
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
                titleLabel = "Flank Angle",
                label = "Flank Angle",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 2,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Cone Angle",
                label = "Cone Half Angle",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 3,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Min Dist",
                label = "Min Distance",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 4,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Max Dist",
                label = "Max Distance",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 5,
                allowedTypes = new[] { typeof(float) }
            },
            new DynamicParamDescriptor
            {
                titleLabel = "Maintain Dist",
                label = "Maintain Distance",
                kind = DynamicParamKind.ScriptableObjectConstant,
                index = 6,
                allowedTypes = new[] { typeof(float) }
            },
        };

        private int outputSlot = -1;
        private int targetSlot = -1;
        private float flankAngle;
        private float coneAngle;
        private float minDist;
        private float maxDist;
        private float maintainDistance;
        private Transform cachedOrigin = null;

        public override void DeserializeParameters(
            ReadOnlySpan<FieldData> fields,
            string[] fieldTypeNames,
            object[] boxedConstants)
        {
            int fieldIndex = 0;

            outputSlot = ReadVariableSlot(fields, ref fieldIndex);
            targetSlot = ReadVariableSlot(fields, ref fieldIndex);

            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                flankAngle = fields[fieldIndex++].GetFloat();
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                coneAngle = fields[fieldIndex++].GetFloat();
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                minDist = fields[fieldIndex++].GetFloat();
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                maxDist = fields[fieldIndex++].GetFloat();
            if (fieldIndex < fields.Length && fields[fieldIndex].IsConstant)
                maintainDistance = fields[fieldIndex].GetFloat();
        }

        protected override void OnInitialize()
        {
            NavMeshAgent agent = GetComponentFromBB<NavMeshAgent>();
            cachedOrigin = agent?.transform;
        }

        public override NodeState Execute()
        {
            if (outputSlot < 0 || targetSlot < 0)
                return NodeState.FAILURE;

            object targetObj = BB.GetBoxed(targetSlot);
            Vector3 targetPos;
            if (targetObj is Transform t)
                targetPos = t.position;
            else if (targetObj is Vector3 v3)
                targetPos = v3;
            else
                return NodeState.FAILURE;

            Vector3 origin = cachedOrigin.position;
            Vector3 flankPos = CalculateFlankPosition(origin, targetPos, cachedOrigin);

            BB.SetBoxed(outputSlot, flankPos);
            return NodeState.SUCCESS;
        }

        private Vector3 CalculateFlankPosition(Vector3 origin, Vector3 target, Transform agentTransform)
        {
            Vector3 toTarget = target - origin;
            float distanceToTarget = toTarget.magnitude;

            // Target's "left" from target's perspective (looking at agent).
            // Cross(toTarget, up) = target's left on XZ plane.
            Vector3 targetLeft = Vector3.Cross(toTarget.normalized, Vector3.up).normalized;
            // Determine if target is on agent's right or left using agent's local right vector.
            // Flank the OPPOSITE side: target on right → flank left, target on left → flank right.
            bool targetIsOnAgentRight = Vector3.Dot(agentTransform.right, toTarget) > 0f;
            Vector3 perpDir = targetIsOnAgentRight ? targetLeft : -targetLeft;

            // Rotate perpendicular toward target by flankAngle (0° = pure side, 90° = direct)
            Vector3 baseDir = Vector3.Slerp(perpDir, toTarget.normalized, flankAngle / 90f);

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
                dirBlend = 1f - t;                             // flank dir at maintainDist → backward at 0
            }
            else
            {
                float t = Mathf.Clamp01(distRatio - 1f);      // 0 at maintainDist, 1 at 2×
                coneScale = Mathf.Lerp(1f, 0.3f, t);          // narrow cone at range
                clampedAlpha = Mathf.Lerp(0.1f, 1f, t);       // long move at range
                dirBlend = 1f;                                 // always flank dir beyond maintainDist
            }

            // Lerp base direction from backward (at distance 0) to flank dir (at maintainDist+)
            Vector3 backward = -toTarget.normalized;
            Vector3 blendedBaseDir = Vector3.Lerp(backward, baseDir, dirBlend);

            // Random spread within cone
            Quaternion randomRotation = Quaternion.Euler(
                Random.Range(-coneAngle * coneScale, coneAngle * coneScale),
                Random.Range(-coneAngle * coneScale, coneAngle * coneScale),
                0f);
            Vector3 randomDir = randomRotation * blendedBaseDir;

            float randomDistance = Random.Range(
                minDist * clampedAlpha,
                maxDist * clampedAlpha);

            return origin + randomDir.normalized * randomDistance;
        }
    }
}
