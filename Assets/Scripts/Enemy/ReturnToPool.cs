using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReturnToPool : MonoBehaviour
{
    private ObjectPool objectPool;
    private void Awake()
    {
        objectPool = FindFirstObjectByType<ObjectPool>();
    }

    [SerializeField] private float lifeTime;
    private void Update()
    {
        StartCoroutine(TimeLife());
    }

    private IEnumerator TimeLife()
    {
        yield return new WaitForSeconds(lifeTime);
        if (objectPool != null)
        {
            this.gameObject.SetActive(false);
            objectPool.ReturnGameObject(this.gameObject);
        }
    }
}
