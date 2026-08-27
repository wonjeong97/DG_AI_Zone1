using UnityEngine;

namespace Scenes
{
    // 씬 매니저들이 공유하는 UI 이펙트 머티리얼 — 셰이더당 1회만 생성해 재사용 (씬 재방문 시 인스턴스 누적 방지)
    public static class UiEffects
    {
        private static Material _grayscaleMaterial;
        private static bool _grayscaleLookedUp;

        public static Material GrayscaleMaterial
        {
            get
            {
                if (!_grayscaleLookedUp)
                {
                    _grayscaleLookedUp = true;
                    Shader shader = Shader.Find(Constants.ResourcePaths.GrayscaleShader);
                    if (shader) _grayscaleMaterial = new Material(shader);
                }
                return _grayscaleMaterial;
            }
        }
    }
}
