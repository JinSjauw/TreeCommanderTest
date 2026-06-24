using UnityEngine;

public class DeathHandler : MonoBehaviour
{
    private static readonly Material[] EmptyMaterials = new Material[0];

    [SerializeField] private Transform transformToRemove;
    [SerializeField] private Transform meshRootTransform;

    [SerializeField] private Material materialToApply;
    [SerializeField] private Transform corpsePrefab;

    private ObjectPool pool;

    private void CloneMesh(Transform root, Transform clone) 
    {
        foreach (Transform child in root)
        {
            Transform cloneChild = clone.Find(child.name);
            if (cloneChild != null)
            {
                cloneChild.position = child.position;
                cloneChild.rotation = child.rotation;
                cloneChild.localScale = child.localScale;

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
        corpseTransform.localScale = meshRootTransform.localScale;
        corpseTransform.position = meshRootTransform.position;

        CloneMesh(meshRootTransform, corpseTransform);

        pool.ReturnGameObject(transformToRemove.gameObject);
    }
}
