using System.Collections.Generic;

namespace DG.Game.Runtime
{
    // Command 블록에 연결된 Value 블록의 값에 따라 점수를 부여한다.
    // 각 Command 블록당 1회 채점 — 반복/조건 내부 블록도 배치 기준으로 1회.
    public static class BlockScorer
    {
        // 질문 시간대별 정답 방향 (정답 5점, 나머지 방향 1점)
        private readonly static Dictionary<string, string> CorrectDirection = new Dictionary<string, string>
        {
            ["아침 8시"]  = "동쪽",
            ["오전 10시"] = "동쪽",
            ["정오"]      = "남쪽",
            ["오후 2시"]  = "서쪽",
            ["오후 4시"]  = "서쪽",
        };

        private readonly static Dictionary<string, int> AngleScore = new Dictionary<string, int>
        {
            ["30도"] = 3,
            ["45도"] = 5,
            ["60도"] = 1,
        };

        private readonly static Dictionary<string, int> CountScore = new Dictionary<string, int>
        {
            ["20개"] = 1,
            ["40개"] = 3,
            ["60개"] = 5,
        };

        public static int ScoreProgram(List<BlockInstruction> instructions, string questionValueKey, string levelName = null)
        {
            int total = 0;
            foreach (var instr in instructions)
            {
                switch (instr)
                {
                    case CommandInstruction cmd:
                        total += ScoreCommand(cmd, questionValueKey, levelName);
                        break;
                    case RepeatInstruction rep when rep.Body is not null:
                        total += ScoreProgram(rep.Body, questionValueKey, levelName);
                        break;
                    case IfInstruction ifInstr:
                        if (ifInstr.Then is not null) total += ScoreProgram(ifInstr.Then, questionValueKey, levelName);
                        if (ifInstr.Else is not null) total += ScoreProgram(ifInstr.Else, questionValueKey, levelName);
                        break;
                }
            }
            return total;
        }

        // 방향 정답 점수 (오답은 1점)
        private const int DirectionCorrectScore = 5;

        // ── 최고 점수 값 조회 (AI 코딩 결과 표시용) ─────────────────
        // 프로그램 최고 점수 — 방향 정답 + 각도/개수 최고점 합
        public static int GetMaxScore()
            => DirectionCorrectScore + AngleScore[GetBestAngle()] + CountScore[GetBestCount()];

        public static string GetBestDirection(string questionValueKey, string levelName = null)
            => Constants.Questions.GetCorrectDirection(levelName, questionValueKey);

        public static string GetBestAngle() => MaxScoreKey(AngleScore);
        public static string GetBestCount() => MaxScoreKey(CountScore);

        private static string MaxScoreKey(Dictionary<string, int> table)
        {
            string bestKey = null;
            int bestScore = int.MinValue;
            foreach (var pair in table)
                if (pair.Value > bestScore)
                {
                    bestScore = pair.Value;
                    bestKey = pair.Key;
                }
            return bestKey;
        }

        // ── 프로그램에서 플레이어가 조립한 값 추출 ──────────────────
        // 같은 타입의 Command가 여러 개면 마지막 값이 남는다.
        public static (string direction, string angle, string count) ExtractValues(List<BlockInstruction> instructions)
        {
            var values = new string[3];
            CollectValues(instructions, values);
            return (values[0], values[1], values[2]);
        }

        private static void CollectValues(List<BlockInstruction> instructions, string[] values)
        {
            foreach (var instr in instructions)
            {
                switch (instr)
                {
                    case CommandInstruction cmd when cmd.Value is not null:
                        if (cmd.ValueKind == ValueKind.Direction) values[0] = cmd.Value;
                        else if (cmd.ValueKind == ValueKind.Angle) values[1] = cmd.Value;
                        else if (cmd.ValueKind == ValueKind.Count) values[2] = cmd.Value;
                        break;
                    case RepeatInstruction rep when rep.Body is not null:
                        CollectValues(rep.Body, values);
                        break;
                    case IfInstruction ifInstr:
                        if (ifInstr.Then is not null) CollectValues(ifInstr.Then, values);
                        if (ifInstr.Else is not null) CollectValues(ifInstr.Else, values);
                        break;
                }
            }
        }

        public static int ScoreCommand(CommandInstruction cmd, string questionValueKey, string levelName = null)
        {
            if (cmd.Value is null) return 0;

            switch (cmd.ValueKind)
            {
                case ValueKind.Direction:
                    string correct = Constants.Questions.GetCorrectDirection(levelName, questionValueKey);
                    return correct != null && cmd.Value == correct ? DirectionCorrectScore : 1;
                case ValueKind.Angle:
                    return AngleScore.TryGetValue(cmd.Value, out var angle) ? angle : 0;
                case ValueKind.Count:
                    return CountScore.TryGetValue(cmd.Value, out var count) ? count : 0;
                default:
                    return 0;
            }
        }
    }
}
