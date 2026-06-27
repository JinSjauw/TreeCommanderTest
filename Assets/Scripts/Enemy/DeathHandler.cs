using Unity.VisualScripting;
using UnityEngine;

public class DeathHandler : MonoBehaviour
{
    private static readonly Material[] EmptyMaterials = new Material[0];

    [SerializeField] private Transform transformToRemove;
    [SerializeField] private Transform meshRootTransform;

    [SerializeField] private Material materialToApply;
    [SerializeField] private Transform corpsePrefab;

    [SerializeField] private float transformToReturnTime = 1f;
    private bool returnTimerActive = false;
    private float returnTimer = 0f;

    private ObjectPool pool;

    private MeshRenderer[] meshRenderers;

    private void Awake()
    {
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
    }

    private void OnEnable()
    {
        returnTimerActive = false;
        returnTimer = 0f;

        EnableMeshRenderers(true);
    }

    private void Update()
    {
        if (returnTimerActive)
        {
            returnTimer += Time.deltaTime;

            if (returnTimer >= transformToReturnTime)
            {
                returnTimerActive = false;
                returnTimer = 0f;

                pool.ReturnGameObject(transformToRemove.gameObject);
            }
        }
    }

    private void EnableMeshRenderers(bool state)
    {
        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            meshRenderer.enabled = state;
        }
    }

    private void CloneMesh(Transform root, Transform clone) 
    {
        foreach (Transform child in root)
        {
            Transform cloneChild = clone.Find(child.name);
            if (cloneChild != null)
            {
                cloneChild.position = child.position;
                cloneChild.rotation = child.rotation;
                // Match actual world-space scale by accounting for parent hierarchy
                Vector3 targetWorldScale = child.lossyScale;
                Transform cloneParent = cloneChild.parent;
                if (cloneParent != null)
                {
                    Vector3 parentWorldScale = cloneParent.lossyScale;
                    cloneChild.localScale = new Vector3(
                        Mathf.Approximately(parentWorldScale.x, 0f) ? targetWorldScale.x : targetWorldScale.x / parentWorldScale.x,
                        Mathf.Approximately(parentWorldScale.y, 0f) ? targetWorldScale.y : targetWorldScale.y / parentWorldScale.y,
                        Mathf.Approximately(parentWorldScale.z, 0f) ? targetWorldScale.z : targetWorldScale.z / parentWorldScale.z
                    );
                }
                else
                {
                    cloneChild.localScale = targetWorldScale;
                }

                if(cloneChild.TryGetComponent(out MeshRenderer meshRenderer)) 
                {
                    meshRenderer.sharedMaterials = EmptyMaterials;
                    meshRenderer.material = materialToApply;
                }

                CloneMesh(child, cloneChild);
            }
        }
    }

    public void SetPool(ObjectPool objectPool)
    {
        pool = objectPool;
    }

    public void SpawnCorpse() 
    {
        if (pool == null)
        {
            pool = ObjectPool.Instance;
        }

        if (pool == null)
        {
            Debug.LogError($"[DeathHandler] No ObjectPool found on {name}. Cannot spawn corpse.");
            return;
        }

        GameObject corpseObject = pool.GetObject(corpsePrefab.gameObject);
        Transform corpseTransform = corpseObject.transform;
        corpseTransform.localScale = meshRootTransform.lossyScale;
        corpseTransform.position = meshRootTransform.position;

        CloneMesh(meshRootTransform, corpseTransform);

        //Start return timer
        //pool.ReturnGameObject(transformToRemove.gameObject);
        EnableMeshRenderers(false);
        returnTimerActive = true;
    }
}
