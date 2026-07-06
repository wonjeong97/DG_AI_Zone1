using UnityEngine;

namespace DG.App
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            GameObject prefab = Resources.Load<GameObject>("App");
            if (!prefab)
            {
                Debug.LogError("[GameBootstrap] App prefab not found in Resources.");
                return;
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = "App";
            Object.DontDestroyOnLoad(instance);
        }
    }
}
