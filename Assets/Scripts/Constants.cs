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

            // 값 슬롯 없는 Command 블록 (개방하기 …)
            public const float CommandNoValueWidth  = 315f;
            public const float CommandNoValueHeight = 119f;

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
            // 소켓 GameObject 이름 — transform.Find 탐색 키로 여러 파일에서 공유
            public const string ChainOutName     = "ChainOutSocket";
            public const string ChainInName      = "ChainInSocket";
            public const string InnerName        = "InnerSocket";
            public const string InnerBottomName  = "InnerBottomSocket";
            public const string ValueOutName     = "ValueOutSocket";
            public const string ValueInName      = "ValueInSocket";
            public const string ConditionOutName = "ConditionOutSocket";
            public const string ConditionInName  = "ConditionInSocket";

            // 세로 체인 (ChainOut: 하단 중앙 앵커 / ChainIn: 상단 중앙 앵커)
            public readonly static UnityEngine.Vector2 StartChainOut   = new(-68.5f, 16f);
            public readonly static UnityEngine.Vector2 FlowChainOut    = new(-96f, 8f);
            public readonly static UnityEngine.Vector2 CommandChainOut = new(-78.3f, 11.5f);
            public readonly static UnityEngine.Vector2 EndChainIn      = new(-72f, -16f);
            public readonly static UnityEngine.Vector2 FlowChainIn     = new(-90f, -17.5f);
            public readonly static UnityEngine.Vector2 CommandChainIn  = new(-78f, -16f);

            // 값 슬롯 없는 Command (개방하기 …) — 폭이 좁아 체인 소켓 x를 별도 보정
            public readonly static UnityEngine.Vector2 CommandNoValueChainOut = new(-62f, 11.5f);
            public readonly static UnityEngine.Vector2 CommandNoValueChainIn  = new(-62f, -16f);

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
            // 실제 소스는 FlowControlBlock 프리팹의 Header_Flow/ValueOutSocket (여기는 기록용)
            public readonly static UnityEngine.Vector2 FlowHeaderValueOut = new(-22f, 10f);
        }

        // ── 레벨별 문제 출제 및 정답 전용 센터 ───────────────────────
        public static class Questions
        {
            public struct QuestionData
            {
                public string QuestionText;
                public string ValueKey;      // 시간대("아침 8시") 또는 바람 방향("동쪽")
                public string CorrectAnswer;  // 정답 방향("동쪽")
            }

            // 태양광 레벨 (시간대별 정답 방향)
            public readonly static string[] SolarTimes = { "아침 8시", "오전 10시", "정오", "오후 2시", "오후 4시" };
            public readonly static System.Collections.Generic.Dictionary<string, string> SolarAnswers =
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["아침 8시"]  = "동쪽",
                    ["오전 10시"] = "동쪽",
                    ["정오"]      = "남쪽",
                    ["오후 2시"]  = "서쪽",
                    ["오후 4시"]  = "서쪽",
                };

            // 풍력 레벨 (바람 방향별 정답 날개 방향)
            public readonly static string[] WindDirections = { "동쪽", "서쪽", "남쪽", "북쪽" };
            public readonly static System.Collections.Generic.Dictionary<string, string> WindAnswers =
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["동쪽"] = "동쪽",
                    ["서쪽"] = "서쪽",
                    ["남쪽"] = "남쪽",
                    ["북쪽"] = "북쪽",
                };

            public static QuestionData GenerateQuestion(string levelName)
            {
                if (!string.IsNullOrEmpty(levelName) && levelName.Contains("풍력"))
                {
                    string dir = WindDirections[UnityEngine.Random.Range(0, WindDirections.Length)];
                    string text = $"이곳은 바람이 <color=yellow>{dir}에서 불고 있습니다.</color>\n풍차의 날개 방향이 어디로 향해 있어야 할까요?\n알맞은 블록을 사용하여 코딩해봅시다.";
                    string ans = WindAnswers.TryGetValue(dir, out var a) ? a : dir;
                    return new QuestionData { QuestionText = text, ValueKey = dir, CorrectAnswer = ans };
                }
                else
                {
                    string time = SolarTimes[UnityEngine.Random.Range(0, SolarTimes.Length)];
                    string text = $"<color=yellow>현재 {time}</color>입니다. 태양광 패널이 어느 방향으로\n향해 있어야 할까요? 알맞은 블록을 사용하여 코딩해봅시다.";
                    string ans = SolarAnswers.TryGetValue(time, out var a) ? a : "동쪽";
                    return new QuestionData { QuestionText = text, ValueKey = time, CorrectAnswer = ans };
                }
            }

            public static string GetCorrectDirection(string levelName, string valueKey)
            {
                if (string.IsNullOrEmpty(valueKey)) return null;

                if (!string.IsNullOrEmpty(levelName) && levelName.Contains("풍력"))
                    return WindAnswers.TryGetValue(valueKey, out var windAns) ? windAns : valueKey;

                return SolarAnswers.TryGetValue(valueKey, out var solarAns) ? solarAns : null;
            }
        }
    }
}
