using System.Collections.Generic;

namespace DG.Game.Runtime
{
    // Command 블록에 연결된 Value 블록의 값에 따라 점수를 부여한다.
    // 각 Command 블록당 1회 채점 — 반복/조건 내부 블록도 배치 기준으로 1회.
    public static class BlockScorer
    {
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
                    case FunctionInstruction fn when fn.Body is not null:
                        total += ScoreProgram(fn.Body, questionValueKey, levelName);
                        break;
                    case IfInstruction ifInstr:
                        if (ifInstr.Then is not null) total += ScoreProgram(ifInstr.Then, questionValueKey, levelName);
                        if (ifInstr.Else is not null) total += ScoreProgram(ifInstr.Else, questionValueKey, levelName);
                        break;
                }
            }
            return total;
        }

        // ── 최고 점수 값 조회 (AI 코딩 결과 표시용) ─────────────────
        // 프로그램 최고 점수 — 방향 정답 + 각도/개수 최고점 합
        public static int GetMaxScore()
            => Constants.Scores.DirectionCorrectScore + Constants.Scores.AngleScore[GetBestAngle()] + Constants.Scores.CountScore[GetBestCount()];

        public static string GetBestDirection(string questionValueKey, string levelName = null)
            => Constants.Questions.GetCorrectDirection(levelName, questionValueKey);

        public static string GetBestAngle() => MaxScoreKey(Constants.Scores.AngleScore);
        public static string GetBestCount() => MaxScoreKey(Constants.Scores.CountScore);

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
                    case FunctionInstruction fn when fn.Body is not null:
                        CollectValues(fn.Body, values);
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
                    return correct != null && cmd.Value == correct ? Constants.Scores.DirectionCorrectScore : 1;
                case ValueKind.Angle:
                    return Constants.Scores.AngleScore.TryGetValue(cmd.Value, out var angle) ? angle : 0;
                case ValueKind.Count:
                    return Constants.Scores.CountScore.TryGetValue(cmd.Value, out var count) ? count : 0;
                default:
                    return 0;
            }
        }
    }
}
