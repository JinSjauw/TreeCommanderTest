using UnityEngine;

public static class Services
{
    // Held reference - populated at bootstrap
    private static InputReader input;

    public static InputReader Input
    {
        get
        {
            if (input == null)
                Debug.LogWarning("Services.Input accessed before initialization!");
            return input;
        }
    }

    // Called once at game start
    public static void Initialize(InputReader inputReader)
    {
        input = inputReader;
        input.Initialize();
    }

    // Cleanup
    public static void Shutdown()
    {
        input?.Shutdown();
        input = null;
    }
}
