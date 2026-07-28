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

        // ── 블록 라벨 (아트·프리팹 분기 키) ───────────────────────
        // FlowControl은 만약/반복하기가 같은 카테고리라 라벨로만 구분된다
        public static class BlockLabels
        {
            public const string While = "반복하기";
            public const string If    = "만약";
        }

        // ── 스토리 텍스트 한 줄씩 올라오는 연출 타이밍 ─────────────
        public static class StoryLine
        {
            public const float StoryLineMoveDuration = 0.7f;
            public const float StoryLineInterval     = 0.35f;
            public const float StoryLineYOffset      = 22.0f;
        }

        // ── 블록 크기 ───────────────────────────────────────────
        public static class Blocks
        {
            // 단순 블록 기본 크기
            public const float DefaultWidth  = 220f;
            public const float DefaultHeight = 56f;

            // 시작하기 / 완성하기 (Control) — Start/End 스프라이트 네이티브 사이즈
            public const float StartWidth  = 361f;
            public const float StartHeight = 121f;
            public const float EndWidth    = 361f;
            public const float EndHeight   = 101f;

            // Value 블록 (30도, 동쪽 …) — Value 스프라이트 네이티브 사이즈
            public const float ValueWidth  = 361f;
            public const float ValueHeight = 101f;

            // Command 블록 (태양광 패널의 각도 …) — Command 스프라이트 네이티브 사이즈
            public const float CommandWidth  = 381f;
            public const float CommandHeight = 121f;

            // 값 슬롯 없는 Command 블록 (개방하기 …) — CommandNoValue 스프라이트 네이티브 사이즈
            public const float CommandNoValueWidth  = 361f;
            public const float CommandNoValueHeight = 121f;

            // FlowControl(반복/만약) — 원본 아트 254px 폭 기준 확대 배율.
            // transform 스케일 대신 프레임 크기·9-slice 보더 두께·라벨 크기를 함께 키워
            // 내부 소켓에 연결되는 자식 블록 크기에는 영향을 주지 않는다
            public const float FlowScale        = 367f / 254f; // 폭 367 기준
            public const float FlowWidth        = 254f * FlowScale;
            public const float FlowHeaderHeight = 78f * FlowScale;
            public const float FlowElseHeight   = 44f * FlowScale;
            public const float FlowFooterHeight = 83f * FlowScale;

            // FlowControl 내부 컨테이너 최소 높이 (내부 블록 0개일 때)
            public const float FlowInnerMinHeight = 44f;

            // 반복하기(while) — while.png 네이티브 사이즈. 헤더/푸터 높이는 9-slice 보더와 일치해야 한다
            // (아트 361x321: 헤더 0~120, 늘어나는 중단 121~199, 푸터 200~320)
            public const float WhileWidth        = 361f;
            public const float WhileHeaderHeight = 121f;
            public const float WhileFooterHeight = 121f;

            // 만약(if) — if.png 네이티브 사이즈 (아트 381x321: 헤더 0~120, 중단 121~199, 푸터 200~320).
            // 헤더 우측에 조건 슬롯 탭이 있음. 하단 체인 탭 추가로 while과 동일한 헤더/푸터 높이
            public const float IfWidth        = 381f;
            public const float IfHeaderHeight = 121f;
            public const float IfFooterHeight = 121f;

            // Logic(그리고/또는) 블록 확대 배율 — Logic는 네이티브 사이즈로 쓰므로 1
            public const float LogicScale = 1f;

            // Condition(전기 과부하 등 ValueKind.None 조건) — Condition 네이티브 사이즈.
            // Logic와 좌우 노치/탭 위치가 동일해 서로 어긋남 없이 체인된다
            public const float ConditionWidth  = 381f;
            public const float ConditionHeight = 101f;

            // 함수 정의(FuncDef) — FuncBody 네이티브 사이즈 (아트 361x301: 헤더 0~120, 중단 121~199,
            // 푸터 200~300). 체인에 연결되지 않는 독립 컨테이너라 상단 체인 노치·하단 체인 탭이 아트에 없어
            // while보다 푸터가 20 짧다. 내부 소켓은 while과 동일 위치(중심 x=60, 폭 361)
            public const float FuncBodyWidth        = 361f;
            public const float FuncBodyHeaderHeight = 121f;
            public const float FuncBodyFooterHeight = 101f;

            // 블록 라벨 텍스트 크기 (전 블록 공통 — 프리팹에도 같은 값이 베이크됨)
            public const float LabelFontSize = 34f;
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
            public readonly static UnityEngine.Vector2 StartChainOut   = new(-120.5f, 10f);
            public readonly static UnityEngine.Vector2 FlowChainOut    = new(-96f, 8f);
            public readonly static UnityEngine.Vector2 CommandChainOut = new(-150f, 10f);
            public readonly static UnityEngine.Vector2 EndChainIn      = new(-120.5f, -10f);
            public readonly static UnityEngine.Vector2 FlowChainIn     = new(-90f, -17.5f);
            public readonly static UnityEngine.Vector2 CommandChainIn  = new(-150f, -10f);

            // 반복하기(while) — while.png 전용. 노치/탭이 모두 x=60(폭 361) 중심이라 Start/End와 같은 x
            public readonly static UnityEngine.Vector2 WhileChainOut = new(-120.5f, 10f);
            public readonly static UnityEngine.Vector2 WhileChainIn  = new(-120.5f, -10f);

            // 만약(if) — if.png 전용. 노치는 x=60이지만 폭이 381이라 x 보정값이 다르다
            public readonly static UnityEngine.Vector2 IfChainOut = new(-130.5f, 10f);
            public readonly static UnityEngine.Vector2 IfChainIn  = new(-130.5f, -10f);

            // 값 슬롯 없는 Command (개방하기 …) / Function(함수 사용) 공용 — CommandNoValue와
            // Func가 노치/탭 위치까지 동일(중심 x=40.5, 폭 361)해서 그대로 공유 가능
            public readonly static UnityEngine.Vector2 CommandNoValueChainOut = new(-140f, 10f);
            public readonly static UnityEngine.Vector2 CommandNoValueChainIn  = new(-140f, -10f);

            // 값 연결 (ValueOut: 우측 중앙 앵커 / ValueIn: 좌측 중앙 앵커)
            public readonly static UnityEngine.Vector2 CommandValueOut   = new(-10.5f, 10f);
            public readonly static UnityEngine.Vector2 ValueValueIn      = new(9.5f, 0f);
            // Condition 전용 — Logic와 노치 위치가 같아(x=60, 폭 381) 같은 좌측 오프셋을 공유
            public readonly static UnityEngine.Vector2 ConditionValueIn  = new(10f, 0f);

            // 조건 체인 (Condition 블록의 수평 연결) — Condition 전용, Logic와 동일 노치/탭 위치
            public readonly static UnityEngine.Vector2 ConditionIn  = new(10f, 0f);
            public readonly static UnityEngine.Vector2 ConditionOut = new(-10f, 0f);

            // Logic(그리고/또는) — Logic 전용. 좌측 노치/우측 탭 모두 깊이 20, 중심이 세로 정중앙(y=0)
            public readonly static UnityEngine.Vector2 LogicConditionIn  = new(10f, 0f);
            public readonly static UnityEngine.Vector2 LogicConditionOut = new(-10f, 0f);

            // FlowControl 내부 (Inner 컨테이너 기준)
            public readonly static UnityEngine.Vector2 FlowInner       = new(-44f, 28f);
            public readonly static UnityEngine.Vector2 FlowInnerBottom = new(-44f, -46f);
            // 실제 소스는 FlowControlBlock 프리팹의 Header_Flow/ValueOutSocket (여기는 기록용)
            public readonly static UnityEngine.Vector2 FlowHeaderValueOut = new(-22f, 10f);
        }

        // ── 카테고리(블록) 색상 — 카테고리 버튼 & 스프라이트 미지정 블록 대체색 ──
        public static class CategoryColors
        {
            public readonly static UnityEngine.Color Control         = new UnityEngine.Color32( 51,  51,  51, 255);
            public readonly static UnityEngine.Color Command         = new UnityEngine.Color32(213,  96, 180, 255);
            public readonly static UnityEngine.Color Value           = new UnityEngine.Color32( 69, 153, 217, 255);
            public readonly static UnityEngine.Color FlowControl     = new UnityEngine.Color32(235, 145,  20, 255);
            public readonly static UnityEngine.Color ConditionAction = new UnityEngine.Color32(219, 102, 161, 255);
            public readonly static UnityEngine.Color Action          = new UnityEngine.Color32( 71, 184, 168, 255);
            public readonly static UnityEngine.Color Logic           = new UnityEngine.Color32( 84, 186,  92, 255);
            public readonly static UnityEngine.Color Condition       = new UnityEngine.Color32( 69, 153, 217, 255);
            public readonly static UnityEngine.Color Function        = new UnityEngine.Color32(133,  36,  69, 255);
            public readonly static UnityEngine.Color FunctionDef     = new UnityEngine.Color32(133,  36,  69, 255);
            public readonly static UnityEngine.Color Default         = new UnityEngine.Color32(255, 255, 255, 255);
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
