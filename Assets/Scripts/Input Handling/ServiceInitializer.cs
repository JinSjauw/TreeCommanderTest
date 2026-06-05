using UnityEngine;

public class ServiceInitializer : MonoBehaviour
{
    [SerializeField] private InputReader inputReader;

    private static bool isInitialized = false;

    private void Awake()
    {
        if (isInitialized)
        {
            Destroy(gameObject);
            return;
        }

        // Single initialization point
        Services.Initialize(inputReader);

        DontDestroyOnLoad(gameObject);
        isInitialized = true;
    }

    private void OnDestroy()
    {
        Services.Shutdown();
    }

    #if !UNITY_EDITOR

    private void OnApplicationQuit()
    {
        Services.Shutdown();
    }

    #endif
}
