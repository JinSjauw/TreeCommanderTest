using UnityEngine;

public class ProjectileTrailManager : MonoBehaviour
{

    private TrailRenderer[] trailRenderers;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        trailRenderers = GetComponentsInChildren<TrailRenderer>();
    }
    private void OnEnable()
    {
        EnableTrailRenderers(true);
    }
    private void OnDisable()
    {
        EnableTrailRenderers(false);
    }

    private void EnableTrailRenderers(bool enable)
    {
        foreach (TrailRenderer trailRenderer in trailRenderers)
        {
            trailRenderer.enabled = enable;
            if(!enable)
            {
                trailRenderer.Clear();
            }
        }
    }
}
