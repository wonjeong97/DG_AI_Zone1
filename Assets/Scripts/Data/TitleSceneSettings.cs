using System;

namespace Data
{
    // StreamingAssets/0_Title.json 매핑 — 0_Title 씬의 연출 타이밍을 재빌드 없이 조정
    [Serializable]
    public class TitleSceneSettings
    {
        public float qrFadeDuration = 1.2f;
        public float qrBlinkMinAlpha = 0.3f;
    }
}
