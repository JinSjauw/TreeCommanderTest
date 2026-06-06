using UnityEngine;

public class ReturnToPool : MonoBehaviour
{
    private ObjectPool objectPool;

    private void Awake()
    {
        objectPool = FindFirstObjectByType<ObjectPool>();
    }

    [SerializeField] private float lifeTime;

    private float timer;

    private void OnEnable()
    {
        timer = 0f;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            if (objectPool != null)
            {
                this.gameObject.SetActive(false);
                objectPool.ReturnGameObject(this.gameObject);
            }
        }
    }
}
