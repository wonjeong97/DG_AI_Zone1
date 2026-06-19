using UnityEngine;

namespace DG.App
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            var prefab = Resources.Load<GameObject>("App");
            if (prefab == null)
            {
                Debug.LogError("[GameBootstrap] App prefab not found in Resources.");
                return;
            }

            var instance = Object.Instantiate(prefab);
            instance.name = "App";
            Object.DontDestroyOnLoad(instance);
        }
    }
}
