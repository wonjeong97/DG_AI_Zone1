using System;

namespace Data
{
    // StreamingAssets/Json/1_Intro.json 매핑 — 1_Intro 씬의 나레이션 타이밍을 재빌드 없이 조정
    [Serializable]
    public class IntroSceneSettings
    {
        // 나레이션을 튼 순간부터 인트로 문구의 줄이 하나씩 올라오기 시작하는 시각(초) — 글자가 없는 빈 줄은 세지 않는다.
        // 나레이션 파일을 바꾸면 문장마다 말이 시작되는 시각으로 고친다. 값이 줄보다 적으면 남은 줄은 00_Common.json의 storyLineInterval 간격으로 올라온다.
        // 배포 1_Intro.json과 같은 값
        public float[] narrationLineStartTimes = { 0.5f, 3.8f, 8.6f, 13.8f, 17.5f, 20.3f };
    }
}
