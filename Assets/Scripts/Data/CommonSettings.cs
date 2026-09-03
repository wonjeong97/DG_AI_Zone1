using System;

namespace Data
{
    // StreamingAssets/Json/00_Common.json 매핑 — 특정 씬에 속하지 않는 공통 연출 타이밍
    [Serializable]
    public class CommonSettings
    {
        public float sceneTransitionFadeDuration = 0.5f;
        public float panelFadeDuration = 0.5f;

        // 한 줄씩 아래에서 위로 올라오며 페이드인되는 텍스트 연출(StoryLineAnimator) 설정
        public float storyLineMoveDuration = 0.7f;
        public float storyLineInterval = 0.35f;
        public float storyLineYOffset = 22.0f;
    }
}
