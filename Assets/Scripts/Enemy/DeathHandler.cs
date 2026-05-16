using UnityEngine;

public class DeathHandler : MonoBehaviour
{
    [SerializeField] private Transform transformToRemove;
    [SerializeField] private Transform meshRootTransform;

    [SerializeField] private Material materialToApply;
    [SerializeField] private Transform corpsePrefab;

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
                    meshRenderer.material = materialToApply;
                }

                CloneMesh(root, cloneChild);
            }
        }
    }

    public void SpawnCorpse() 
    {
        Transform corpseObject = Instantiate(corpsePrefab);

        corpseObject.localScale = meshRootTransform.localScale;

        CloneMesh(meshRootTransform, corpseObject);

        Destroy(transformToRemove.gameObject);
    }
}
