using System.Collections.Generic;

namespace Game.Runtime
{
    // Command 블록에 연결된 Value 블록의 값에 따라 점수를 부여한다.
    // 각 Command 블록당 1회 채점 — 반복/조건 내부 블록도 배치 기준으로 1회.
    public static class BlockScorer
    {
        public static int ScoreProgram(List<BlockInstruction> instructions, string questionValueKey, string levelName = null)
        {
            bool isHydro = !string.IsNullOrEmpty(levelName) && levelName.Contains("HydroData");

            int total = 0;
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                if (instr is CommandInstruction cmd)
                    total += ScoreCommand(cmd, questionValueKey, levelName);
                else if (isHydro && instr is IfInstruction ifInstr)
                {
                    int conditionScore = ScoreHydroCondition(ifInstr.Condition, questionValueKey);
                    int elseScore = ifInstr.HasElseMarker
                        ? Constants.Scores.HydroElsePlacedScore
                        : Constants.Scores.HydroElseMissingScore;
                    int gateOrderScore = ScoreHydroGateOrder(ifInstr);
                    total += conditionScore * elseScore * gateOrderScore;
                }
            }
            return total;
        }

        // ── 최고 점수 값 조회 (AI 코딩 결과 표시용) ─────────────────
        // 프로그램 최고 점수 — 방향 정답 + 개수 최고점 합
        public static int GetMaxScore(string levelName = null)
        {
            int directionScore = !string.IsNullOrEmpty(levelName) && levelName.Contains("WindData")
                ? Constants.Scores.WindDirectionSameScore
                : Constants.Scores.DirectionCorrectScore;
            return directionScore + Constants.Scores.CountScore[GetBestCount()];
        }

        public static string GetBestDirection(string questionValueKey, string levelName = null)
            => Constants.Questions.GetCorrectDirection(levelName, questionValueKey);

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
            string direction = null, angle = null, count = null;

            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                if (instr is not CommandInstruction cmd || cmd.Value is null) continue;

                switch (cmd.ValueKind)
                {
                    case ValueKind.Direction: direction = cmd.Value; break;
                    case ValueKind.Angle:     angle     = cmd.Value; break;
                    case ValueKind.Count:     count     = cmd.Value; break;
                }
            }

            return (direction, angle, count);
        }

        public static int ScoreCommand(CommandInstruction cmd, string questionValueKey, string levelName = null)
        {
            if (cmd.Value is null) return 0;

            switch (cmd.ValueKind)
            {
                case ValueKind.Direction:
                    string correct = Constants.Questions.GetCorrectDirection(levelName, questionValueKey);
                    if (!string.IsNullOrEmpty(levelName) && levelName.Contains("WindData"))
                        return ScoreWindDirection(cmd.Value, correct);
                    return correct != null && cmd.Value == correct ? Constants.Scores.DirectionCorrectScore : 1;
                case ValueKind.Angle:
                    return Constants.Scores.AngleScore.TryGetValue(cmd.Value, out var angle) ? angle : 0;
                case ValueKind.Count:
                    return Constants.Scores.CountScore.TryGetValue(cmd.Value, out var count) ? count : 0;
                default:
                    return 0;
            }
        }

        // 풍력 레벨 방향 채점 — 문제의 정답 방향과 같으면 3점, 정반대면 1점, 그 외는 2점
        private static int ScoreWindDirection(string chosen, string correct)
        {
            if (correct != null && chosen == correct)
                return Constants.Scores.WindDirectionSameScore;

            if (correct != null && Constants.Questions.OppositeDirection.TryGetValue(correct, out var opposite) && chosen == opposite)
                return Constants.Scores.WindDirectionOppositeScore;

            return Constants.Scores.WindDirectionOtherScore;
        }

        // 수력 레벨 조건 채점 — 만약 블록에 연결한 높이 조건("5m 이상")과 문제 높이("5m")를 비교해
        // 정확히 같으면 5점, 더 낮게 연결했으면 3점, 더 높게 연결했으면 1점
        private static int ScoreHydroCondition(ConditionExpr condition, string questionValueKey)
        {
            if (condition is not SimpleConditionExpr simple) return 0;

            int chosen = ParseMeters(simple.Name);
            int target = ParseMeters(questionValueKey);
            if (chosen < 0 || target < 0) return 0;

            if (chosen == target) return Constants.Scores.HydroExactScore;
            return chosen < target ? Constants.Scores.HydroLowerScore : Constants.Scores.HydroHigherScore;
        }

        // "5m 이상" / "5m" 등 앞자리 숫자를 미터 값으로 파싱
        private static int ParseMeters(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;

            int i = 0;
            while (i < text.Length && char.IsDigit(text[i])) i++;
            return i > 0 && int.TryParse(text.Substring(0, i), out int meters) ? meters : -1;
        }

        private const string HydroOpenCommand  = "개방하기";
        private const string HydroCloseCommand = "폐쇄하기";

        // 수력 레벨 개방/폐쇄 순서 채점 — 개방하기가 Then에, 폐쇄하기가 Else에 있어야 정답(5점).
        // 둘 다 한쪽에 몰려있거나 순서가 반대(폐쇄하기가 Then, 개방하기가 Else)면 1점
        private static int ScoreHydroGateOrder(IfInstruction ifInstr)
        {
            bool correctOrder = ContainsCommand(ifInstr.Then, HydroOpenCommand)
                              && ContainsCommand(ifInstr.Else, HydroCloseCommand);
            return correctOrder ? Constants.Scores.HydroGateOrderCorrectScore : Constants.Scores.HydroGateOrderWrongScore;
        }

        private static bool ContainsCommand(List<BlockInstruction> body, string commandName)
        {
            if (body is null) return false;
            foreach (BlockInstruction instr in body)
                if (instr is CommandInstruction cmd && cmd.Command == commandName)
                    return true;
            return false;
        }
    }
}
