using System;

namespace Data
{
    // StreamingAssets/Json/00_Common.json 매핑 — 특정 씬에 속하지 않는 공통 연출 타이밍
    [Serializable]
    public class CommonSettings
    {
        public float sceneTransitionFadeDuration = 0.5f;
        public float panelFadeDuration = 0.5f;
    }
}
