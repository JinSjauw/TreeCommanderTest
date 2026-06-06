using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    private Dictionary<string, Queue<GameObject>> objectPool = new Dictionary<string, Queue<GameObject>>();

    public GameObject GetObject(GameObject gameObject, bool active = true) 
    {
        if(objectPool.TryGetValue(gameObject.name, out Queue<GameObject> objectList)) 
        {
            if(objectList.Count == 0) 
            {
                return CreateNewObject(gameObject, active);
            }
            else 
            {
                GameObject currentObject = objectList.Dequeue();
                currentObject.SetActive(active);
                return currentObject;
            }
        }
        else { return CreateNewObject(gameObject); }
    }

    private GameObject CreateNewObject(GameObject gameObject, bool active = true) 
    {
        // Instantiate while inactive to prevent Awake/OnEnable from firing
        bool previousActive = gameObject.activeSelf;
        gameObject.SetActive(active);
        GameObject newGameObject = Instantiate(gameObject);
        gameObject.SetActive(previousActive);
        newGameObject.name = gameObject.name;
        return newGameObject;
    }

    public void ReturnGameObject(GameObject gameObject) 
    {
        if(objectPool.TryGetValue(gameObject.name, out Queue<GameObject> objectList)) 
        {
            objectList.Enqueue(gameObject);
        }
        else 
        {
            Queue<GameObject> newObjectQueue = new Queue<GameObject>();
            newObjectQueue.Enqueue(gameObject);
            objectPool.Add(gameObject.name, newObjectQueue);
        }

        gameObject.SetActive(false);
    }

}
