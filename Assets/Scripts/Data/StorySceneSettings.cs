using System;

namespace Data
{
    // StreamingAssets/Json/2_Story.json 매핑 — 2_Story 씬의 연출 타이밍을 재빌드 없이 조정
    [Serializable]
    public class StorySceneSettings
    {
        public float selectedLevelButtonMoveDuration = 0.35f;

        // OutBack 이징의 반동(overshoot) 크기 — DOTween 기본값(1.70158f)과 동일
        public float selectedLevelButtonMoveOvershoot = 1.70158f;
    }
}
