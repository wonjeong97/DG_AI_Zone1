using UnityEngine;

namespace DG.App
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            // 부팅 시점 동기 로드가 반드시 필요 — Addressables.WaitForCompletion은 WebGL에서 지원되지 않음
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
