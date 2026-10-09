using System;

namespace Data
{
    // StreamingAssets/Json/2_Story.json 매핑 — 2_Story 씬의 연출 타이밍을 재빌드 없이 조정
    [Serializable]
    public class StorySceneSettings
    {
        // 고른 레벨 버튼이 스토리 화면 자리로 튀어 들어가는 시간(초)과 OutBack 이징의 반동(overshoot) 크기 — 배포 2_Story.json과 같은 값
        public float selectedLevelButtonMoveDuration = 1f;
        public float selectedLevelButtonMoveOvershoot = 1.3f;
    }
}
