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
                    if (shader)
                        _grayscaleMaterial = new Material(shader);
                    else
                        // 정적 프로퍼티라 로거를 받을 수 없어 Unity 콘솔로 남긴다 — 빌드에서는 Always Included Shaders에 있어야 찾는다
                        Debug.LogWarning($"[UiEffects] '{Constants.ResourcePaths.GrayscaleShader}' 셰이더를 찾지 못해 잠긴 레벨·실패 결과가 흑백으로 보이지 않습니다.");
                }
                return _grayscaleMaterial;
            }
        }
    }
}
