namespace DG
{
    // 프로젝트 전역 상수 — 씬 이름, 블록 크기 등 튜닝 값을 한곳에서 관리
    public static class Constants
    {
        // ── 씬 이름 ─────────────────────────────────────────────
        public static class Scenes
        {
            public const string Title  = "0_Title";
            public const string Intro  = "1_Intro";
            public const string Story  = "2_Story";
            public const string Game   = "3_Game";
            public const string Result = "4_Result";
        }

        // ── 블록 크기 ───────────────────────────────────────────
        public static class Blocks
        {
            // 단순 블록 기본 크기
            public const float DefaultWidth  = 220f;
            public const float DefaultHeight = 56f;

            // 시작하기 / 완성하기 (Control)
            public const float StartWidth  = 353f;
            public const float StartHeight = 127f;
            public const float EndWidth    = 353f;
            public const float EndHeight   = 104f;

            // Value 블록 (30도, 동쪽 …)
            public const float ValueWidth  = 287f;
            public const float ValueHeight = 89f;

            // Command 블록 (태양광 패널의 각도 …)
            public const float CommandWidth  = 371f;
            public const float CommandHeight = 119f;

            // FlowControl(반복/만약) — 원본 아트 254px 폭 기준 확대 배율.
            // transform 스케일 대신 프레임 크기·9-slice 보더 두께·라벨 크기를 함께 키워
            // 내부 소켓에 연결되는 자식 블록 크기에는 영향을 주지 않는다
            public const float FlowScale        = 367f / 254f; // 폭 367 기준
            public const float FlowWidth        = 254f * FlowScale;
            public const float FlowHeaderHeight = 78f * FlowScale;
            public const float FlowElseHeight   = 44f * FlowScale;
            public const float FlowFooterHeight = 83f * FlowScale;

            // FlowControl 내부 컨테이너 최소 높이 (내부 블록 0~1개일 때)
            public const float FlowInnerMinHeight = 18f;
        }

        // ── 소켓 오프셋 (블록별 연결부 위치 미세 조정 값) ─────────
        public static class Sockets
        {
            // 세로 체인 (ChainOut: 하단 중앙 앵커 / ChainIn: 상단 중앙 앵커)
            public readonly static UnityEngine.Vector2 StartChainOut   = new(-68.5f, 16f);
            public readonly static UnityEngine.Vector2 FlowChainOut    = new(-96f, 8f);
            public readonly static UnityEngine.Vector2 CommandChainOut = new(-78.3f, 11.5f);
            public readonly static UnityEngine.Vector2 EndChainIn      = new(-72f, -16f);
            public readonly static UnityEngine.Vector2 FlowChainIn     = new(-90f, -17.5f);
            public readonly static UnityEngine.Vector2 CommandChainIn  = new(-78f, -16f);

            // 값 연결 (ValueOut: 우측 중앙 앵커 / ValueIn: 좌측 중앙 앵커)
            public readonly static UnityEngine.Vector2 CommandValueOut   = new(-8f, 11f);
            public readonly static UnityEngine.Vector2 ValueValueIn      = new(16f, 3.5f);
            public readonly static UnityEngine.Vector2 ConditionValueIn  = new(8f, 0f);

            // 조건 체인 (Condition / Logic 블록의 수평 연결)
            public readonly static UnityEngine.Vector2 ConditionIn  = new(8f, 0f);
            public readonly static UnityEngine.Vector2 ConditionOut = new(-8f, 0f);

            // FlowControl 내부 (Inner 컨테이너 기준)
            public readonly static UnityEngine.Vector2 FlowInner       = new(-44f, 28f);
            public readonly static UnityEngine.Vector2 FlowInnerBottom = new(-44f, -46f);
            public readonly static UnityEngine.Vector2 FlowHeaderValueOut = new(-8f, 0f);
        }
    }
}
