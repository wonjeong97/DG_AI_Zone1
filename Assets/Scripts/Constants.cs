// 프로젝트 전역 상수 — 씬 이름, 비디오/에셋 경로, 방향/점수, 블록 크기, 소켓 등을 한곳에서 관리
public static class Constants
{
    // ── 1. 씬 이름 ─────────────────────────────────────────────
    public static class Scenes
    {
        public const string Title  = "0_Title";
        public const string Intro  = "1_Intro";
        public const string Story  = "2_Story";
        public const string Game   = "3_Game";
        public const string Result = "4_Result";
    }

    // ── 2. 비디오 파일 경로 ──────────────────────────────────────
    public static class VideoPaths
    {
        public const string RobotRelative = "Videos/Robot_260728.webm";

        public static string RobotUrl => System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, RobotRelative);

        // 씬 전환 시 영상이 화면에 드러나기 전 최소 재생 진행률 (%) — 재생 시작 직후의 어색한 첫 프레임을 가림
        public const float MinPlaybackProgressBeforeReveal = 0.01f;
    }

    // ── 3. 리소스 및 에셋 경로 ──────────────────────────────────
    public static class ResourcePaths
    {
        public const string TutorialImageAddress = "Tutorial";
        public const string LabelFontKey         = "GamtanRoadTantan SDF";
        public const string GameSessionKey       = "GameSession";
        public const string SpriteFillShader     = "Custom/UI/SpriteFill";
        public const string GrayscaleShader      = "Custom/UI/Grayscale";
    }

    // ── 4. 방향 명칭 ───────────────────────────────────────────
    public static class Directions
    {
        public const string North = "북쪽";
        public const string East  = "동쪽";
        public const string South = "남쪽";
        public const string West  = "서쪽";
    }

    // ── 5. 점수 관리 ───────────────────────────────────────────
    public static class Scores
    {
        public const int DirectionCorrectScore = 5;

        public static readonly System.Collections.Generic.Dictionary<string, int> AngleScore = new()
        {
            ["30도"] = 3,
            ["45도"] = 5,
            ["60도"] = 1,
        };

        public static readonly System.Collections.Generic.Dictionary<string, int> CountScore = new()
        {
            ["20개"] = 1,
            ["40개"] = 3,
            ["60개"] = 5,
        };
    }

    // ── 6. 카테고리 명칭 ───────────────────────────────────────
    public static class CategoryNames
    {
        public const string Control         = "제어";
        public const string Command         = "동작";
        public const string Value           = "변수";
        public const string FlowControl     = "제어";
        public const string ConditionAction = "조건 동작";
        public const string Action          = "동작";
        public const string Logic           = "논리";
        public const string Condition       = "조건";
        public const string Function        = "함수";
        public const string FunctionDef     = "함수 정의";
    }

    // ── 7. 컴파일러 메시지 ─────────────────────────────────────
    // {0}이 있는 항목은 string.Format용 서식 — 대상 블록 이름이 들어간다
    public static class CompilerMessages
    {
        public const string MissingStartBlock          = "'시작하기' 블록이 코딩 영역에 없습니다";
        public const string StartNotFirst              = "'시작하기' 블록이 첫 번째 블록이어야 합니다";
        public const string MissingConnectedAfterStart = "'시작하기'에 연결된 블록이 없습니다";
        public const string MissingEndBlock            = "'완성하기' 블록으로 끝나지 않았습니다";
        public const string FunctionBetweenStartEnd    = "'함수' 블록을 시작하기와 완성하기 사이에 연결해야 합니다";
        public const string EmptyFunctionDef           = "'함수 정의' 블록 안에 블록을 1개 이상 넣어야 합니다";
        public const string CommandWithoutValueFormat  = "'{0}' 블록에 값 블록이 없습니다";
        public const string IfWithoutConditionFormat   = "'{0}' 블록에 조건이 없습니다";
        public const string EmptyFlowInnerFormat       = "'{0}' 블록 내부에 최소 1개의 블록이 있어야 합니다";
        public const string ControlInsideFlowFormat    = "'{0}' 블록은 제어 블록 내부에 넣을 수 없습니다";
        public const string UnusedBlocksFormat         = "사용되지 않은 블록이 있습니다 ({0}개)";
    }

    // ── 8. 결과 씬 연출 메시지 및 평가 ─────────────────────────
    public static class ResultMessages
    {
        public const string StatusPoor    = "<color=red>부족</color>";
        public const string StatusNormal  = "보통";
        public const string StatusGood    = "<color=#0B7A0B>양호</color>";
        public const string AiCodingStart = "AI가 코딩을 시작합니다";

        // 최고 점수 대비 비율(%)로 전력 수급 상태를 나눈다 — 미만/이상 경계값
        public const float NormalThresholdPercent = 50f;
        public const float GoodThresholdPercent   = 80f;

        public const string EfficiencyFormat = "에너지 효율:{0:D2}%";
        public const string ResultTextFormat = "가동 수: [{0}]\n방향: [{1}]\n\n전력 수급 상태: {2}";

        // 코딩 미완료(스킵)로 표시할 값이 없을 때
        public const string NoResultText = "-\n\n전력 수급 상태: -";

        // 'AI가 코딩을 시작합니다' 뒤 말줄임 애니메이션
        public const int AiCodingDotCycle      = 4;    // 점 0~3개 반복
        public const int AiCodingDotIntervalMs = 400;
    }

    // ── 9. 블록 라벨 (아트·프리팹 분기 키) ───────────────────────
    // FlowControl은 만약/반복하기가 같은 카테고리라 라벨로만 구분된다
    public static class BlockLabels
    {
        public const string While = "반복하기";
        public const string If    = "만약";
        public const string Else  = "아니면";

        // 라벨 부분 일치 판정용 — "반복하기"의 사용자 표기 흔들림을 흡수한다
        public const string RepeatKeyword = "반복";
    }

    // ── 10. 블록 크기 ───────────────────────────────────────────
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

    // ── 11. 소켓 오프셋 (블록별 연결부 위치 미세 조정 값) ─────────
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

    // ── 12. 카테고리(블록) 색상 — 카테고리 버튼 & 스프라이트 미지정 블록 대체색 ──
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

    // ── 13. 블록 어드레서블 키 (프리팹 / 스프라이트) ──────────────
    public static class BlockAssets
    {
        // 프리팹 — 시각 계층(배경·하이라이트·라벨)을 담당하고, 소켓은 런타임에 부착된다
        public const string ValuePrefab          = "ValueBlock";
        public const string ConditionPrefab      = "ConditionBlock";
        public const string CommandPrefab        = "CommandBlock";
        public const string CommandNoValuePrefab = "CommandNoValueBlock";
        public const string StartPrefab          = "StartBlock";
        public const string EndPrefab            = "EndBlock";
        public const string FunctionPrefab       = "FunctionBlock";
        public const string FuncDefPrefab        = "FuncDefBlock";
        public const string WhilePrefab          = "WhileBlock";
        public const string IfPrefab             = "IfBlock";
        public const string FlowControlPrefab    = "FlowControlBlock";
        public const string CategoryButtonPrefab = "CategoryButton";

        // 스프라이트 — 프리팹 없이 코드로 조립하는 블록의 9-slice 아트
        public const string StartSprite           = "Start";
        public const string EndSprite             = "End";
        public const string CommandSprite         = "Command";
        public const string ValueSprite           = "Value";
        public const string FlowControlSprite     = "FlowControl";
        public const string ConditionActionSprite = "ConditionAction";
        public const string ActionSprite          = "Action";
        public const string LogicSprite           = "Logic";
    }

    // ── 14. 블록 내부 자식 오브젝트 이름 (탐색 키) ─────────────────
    public static class BlockParts
    {
        public const string InnerPrefix  = "Inner";   // Inner / Inner_Else … FlowControl 내부 컨테이너
        public const string HeaderPrefix = "Header_"; // Header_반복하기 / Header_아니면 …
        public const string Footer       = "Footer";
        public const string Label        = "Label";
        public const string Sprite       = "Sprite";
        public const string Fill         = "Fill";
        public const string Slot         = "Slot";
        public const string EmptyIndicator = "EmptyIndicator";

        // 하이라이트 오버레이 — 렌더 순서상 블록 본체보다 앞(sibling 0~2)에 놓인다
        public const string Outline        = "SpriteOutline";  // 전체 (컴파일 성공/에러)
        public const string ChainHighlight = "ChainHighlight"; // 하단 35% (체인 스냅)
        public const string ValueHighlight = "ValueHighlight"; // 우측 20% (값 스냅)
    }

    // ── 15. 블록 하이라이트 색상 ──────────────────────────────────
    public static class HighlightColors
    {
        public readonly static UnityEngine.Color Snap    = new(0.1f, 0.9f, 0.3f, 1f);
        public readonly static UnityEngine.Color Success = new(0.1f, 0.9f, 0.3f, 1f);
        public readonly static UnityEngine.Color Error   = new(1f, 0.15f, 0.1f, 1f);

        // 비어있는 내부 슬롯 표시용 반투명 흰색
        public readonly static UnityEngine.Color EmptySlot = new(1f, 1f, 1f, 0.08f);
    }

    // ── 16. 코딩존 고정 배치 ──────────────────────────────────────
    // 시작하기/완성하기는 드래그 대상이 아니라 코딩 패널 좌상단·좌하단에 고정 배치된다
    public static class CodingZoneLayout
    {
        public const float ControlBlockX = 80f;

        // 시작하기: 상단 앵커에서 아래로, 완성하기: 하단 앵커에서 위로 이만큼 띄운다
        public const float ControlBlockYInset = 120f;
    }

    // ── 17. 스토리 연출 ─────────────────────────────────────────
    public static class StoryLine
    {
        public const float StoryLineMoveDuration = 0.7f;
        public const float StoryLineInterval     = 0.35f;
        public const float StoryLineYOffset      = 22.0f;
    }

    // ── 18. 레벨별 문제 출제 및 정답 전용 센터 ───────────────────────
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
                ["아침 8시"]  = Directions.East,
                ["오전 10시"] = Directions.East,
                ["정오"]      = Directions.South,
                ["오후 2시"]  = Directions.West,
                ["오후 4시"]  = Directions.West,
            };

        // 풍력 레벨 (바람 방향별 정답 날개 방향)
        public readonly static string[] WindDirections = { Directions.East, Directions.West, Directions.South, Directions.North };
        public readonly static System.Collections.Generic.Dictionary<string, string> WindAnswers =
            new System.Collections.Generic.Dictionary<string, string>
            {
                [Directions.East]  = Directions.East,
                [Directions.West]  = Directions.West,
                [Directions.South] = Directions.South,
                [Directions.North] = Directions.North,
            };

        public static QuestionData GenerateQuestion(string levelName)
        {
            if (!string.IsNullOrEmpty(levelName) && levelName.Contains("풍력"))
            {
                string dir = WindDirections[UnityEngine.Random.Range(0, WindDirections.Length)];
                string text = $"바람이 <color=yellow>{dir}에서 불고 있습니다.</color>\n풍차의 날개 방향이 어디로 향해 있어야 할까요?";
                string ans = WindAnswers.TryGetValue(dir, out var a) ? a : dir;
                return new QuestionData { QuestionText = text, ValueKey = dir, CorrectAnswer = ans };
            }
            else
            {
                string time = SolarTimes[UnityEngine.Random.Range(0, SolarTimes.Length)];
                string text = $"<color=yellow>현재 {time}</color>입니다.\n태양광 패널이 어느 방향으로 향해 있어야 할까요?";
                string ans = SolarAnswers.TryGetValue(time, out var a) ? a : Directions.East;
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
