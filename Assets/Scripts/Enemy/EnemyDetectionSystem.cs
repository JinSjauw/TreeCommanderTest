using System.Collections.Generic;
using BehaviourTree.Core;
using BehaviourTree.Runtime;
using UnityEngine;

public class EnemyDetectionSystem : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float detectionRadius = 30f;
    [SerializeField] private float firingRadius = 15f;
    [SerializeField] private Transform eyeTransform;

    private Collider[] detectBuffer = new Collider[32];
    private LayerMask groundLayer;

    public LayerMask TargetLayers { get; set; }
    public List<Transform> DetectedTargets { get; private set; } = new List<Transform>();
    public float DetectionRadius => detectionRadius;
    public float FiringRadius => firingRadius;

    private void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");
    }


    public bool DetectTargets()
    {
        DetectedTargets.Clear();

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position, detectionRadius, detectBuffer, TargetLayers);

        if (hitCount == 0)
        {
            return false;
        }

        for (int i = 0; i < hitCount; i++)
        {
            Transform candidate = detectBuffer[i].transform;

            if (candidate == transform || candidate.IsChildOf(transform))
                continue;

            // if (!HasLineOfSight(candidate))
            //     continue;

            DetectedTargets.Add(candidate);
        }

        return DetectedTargets.Count > 0;
    }

    public Transform GetTarget(SelectionStrategy strategy)
    {
        if (DetectedTargets.Count == 0)
            return null;

        return strategy switch
        {
            SelectionStrategy.Nearest => GetNearestTarget(),
            SelectionStrategy.Farthest => GetFarthestTarget(),
            SelectionStrategy.Random => GetRandomTarget(),
            _ => null
        };
    }

    public Transform GetNearestTarget()
    {
        Transform best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < DetectedTargets.Count; i++)
        {
            Transform target = DetectedTargets[i];
            float dist = Vector2.Distance(
                new Vector2(target.position.x, target.position.z),
                new Vector2(transform.position.x, transform.position.z));
            if (dist < bestDist)
            {
                bestDist = dist;
                best = target;
            }
        }

        return best;
    }

    public Transform GetFarthestTarget()
    {
        Transform best = null;
        float bestDist = float.MinValue;

        for (int i = 0; i < DetectedTargets.Count; i++)
        {
            Transform target = DetectedTargets[i];
            float dist = Vector2.Distance(
                new Vector2(target.position.x, target.position.z),
                new Vector2(transform.position.x, transform.position.z));
            if (dist > bestDist)
            {
                bestDist = dist;
                best = target;
            }
        }

        return best;
    }

    public Transform GetRandomTarget()
    {
        if (DetectedTargets.Count == 0)
            return null;

        return DetectedTargets[Random.Range(0, DetectedTargets.Count)];
    }

    public bool HasLineOfSight(Transform target)
    {
        if (target == null || eyeTransform == null)
            return false;

        return !Physics.Linecast(
            eyeTransform.position, target.position, groundLayer);
    }

    public bool IsTargetInRange(Transform target, float range, bool log = false)
    {
        if (target == null)
            return false;

        float distance = Vector2.Distance(new Vector2(target.position.x, target.position.z), new Vector2(transform.position.x, transform.position.z));
        if (log) Debug.Log($"Distance to target: {distance} Range: {range} IsInRange: {distance <= range}");

        return distance <= range;
    }

    public bool TargetInFiringRange(Transform target)
    {
        return IsTargetInRange(target, firingRadius);
    }

    public bool TargetInDetectionRange(Transform target)
    {
        return IsTargetInRange(target, detectionRadius);
    }

    public bool HasLineOfSightToTarget(Transform target)
    {
        return HasLineOfSight(target);
    }

    public float GetDistanceToTarget(Transform target)
    {
        if (target == null)
            return float.MaxValue;

        return Vector2.Distance(
            new Vector2(target.position.x, target.position.z),
            new Vector2(transform.position.x, transform.position.z));
    }
}
