using UnityEngine;

[RequireComponent(typeof(ProjectileUI))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField] private Transform explosionVFX;
    [SerializeField] private Transform impactVFX;
    [SerializeField] private Transform trailVFX;
    [SerializeField] private Transform debrisVFX;
   
    private ProjectileUI projectileUI;
    private ObjectPool pool;

    private Vector3 origin;
    private Vector3 target;
    private Vector3 control;

    private float travelTime;
    private float timer = 0f;

    private float damage;

    private bool isPlayerProjectile = false;
    private bool isInitialized = false;
    private bool isStopped = false;

    private float returnTimer;
    private float returnDelay = 3;

    private static readonly Collider[] OverlapBuffer = new Collider[32];

    [SerializeField] private AnimationCurve damageFallOff;
    [SerializeField] private float damageRadius;

    #region Unity

    // Update is called once per frame
    void Update()
    {
        if (!isInitialized) return;

        if(isStopped) 
        {
            if(returnTimer < returnDelay) 
            {
                returnTimer += Time.deltaTime;
            }
            else 
            {
                ReturnObject();
            }

            if (isPlayerProjectile) return;
        }

        MoveProjectile();
    }

    #endregion

    #region Functions

    public void InitProjectile(Vector3 origin, Vector3 target, Vector3 control, float travelTime, float damage, ObjectPool pool, bool playerProjectile = false)
    {
        this.origin = origin;
        this.target = target;
        this.control = control;
        this.travelTime = travelTime;
        this.damage = damage;
        this.pool = pool;

        isInitialized = true;
        isStopped = false;

        //projectileUI = GetComponent<ProjectileUI>();

        isPlayerProjectile = playerProjectile;

        // if (playerProjectile)
        // {
        //     trailVFX.gameObject.SetActive(true);
        //     debrisVFX.gameObject.SetActive(true);
        // }
        trailVFX.gameObject.SetActive(true);
        debrisVFX.gameObject.SetActive(true);
    }

    private void VisibilityChanged(object sender, bool visible)
    {
        trailVFX.gameObject.SetActive(visible);
    }

    public void EnableMark(bool state) 
    {
        if (isPlayerProjectile) return;

        projectileUI.EnableMark(state);
    }

    public void EnableLockMarks(int index) 
    {
        if (isPlayerProjectile) return;

        projectileUI.EnableLockMarks(index);
    }

    public void ExplodeProjectile() 
    {
        isStopped = true;
        Explode();
        if (!isPlayerProjectile) ReturnObject();
        //fovAgent.SetVisible(false);
        debrisVFX.gameObject.SetActive(false);
    }

    private void MoveProjectile() 
    {
        //Normalize time between 0 and 1

        timer += Time.deltaTime;

        if(timer <= travelTime) 
        {
            float curveAlpha = (timer - 0) / (travelTime - 0);

            Vector3 currentPosition = transform.position;

            //Follow Curvature
            Vector3 newPosition = QuadraticCurve.EvaluateCurve(origin, target, control, curveAlpha);
            
            if(Physics.Linecast(currentPosition, newPosition, out RaycastHit hit, collisionLayers)) 
            {
                
                if (hit.collider.TryGetComponent(out HealthComponent health))
                {
                    GameObject impact = pool.GetObject(impactVFX.gameObject);
                    impact.transform.position = hit.point;
                    impact.transform.up = -transform.forward;
                }
                ExplodeProjectile();

                return;
            }

            //Calculate new forward
            Vector3 newForward = (newPosition - currentPosition).normalized;

            transform.forward = newForward;
            transform.position = newPosition;   
        }
        else 
        {
            ExplodeProjectile();
        }
    }

    private void Explode() 
    {
        //EnableMark(false);
        
        //projectileUI.DisableLockMark();

        pool.GetObject(explosionVFX.gameObject).transform.position = transform.position;

        int numHits = Physics.OverlapSphereNonAlloc(transform.position, damageRadius, OverlapBuffer);
        for (int i = 0; i < numHits; i++)
        {
            if (OverlapBuffer[i].TryGetComponent(out HealthComponent hit))
            {
                float distance = Vector3.Distance(hit.transform.position, transform.position);
                float distanceAlpha = distance / damageRadius;
                hit.TakeDamage(damage * damageFallOff.Evaluate(distanceAlpha));
            }
        }
    }

    private void ReturnObject() 
    {
        isInitialized = false;
        isStopped = false;

        trailVFX.gameObject.SetActive(false);

        timer = 0;
        returnTimer = 0;

        if (pool != null)
        {
            this.gameObject.SetActive(false);
            pool.ReturnGameObject(this.gameObject);
        }
    }

    #endregion
}
