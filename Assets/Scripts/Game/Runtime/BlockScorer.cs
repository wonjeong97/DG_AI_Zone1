using System.Collections.Generic;
using Cysharp.Text;
using Data;
using UnityEngine;
using ZLogger;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Game.Runtime
{
    // 레벨별 규칙으로 프로그램을 채점한다 — 레벨1·2는 Command 블록에 연결한 값 블록으로(각 Command 블록당 1회, 반복/조건 내부도 배치 기준 1회),
    // 레벨3은 첫 만약의 조건·아니면·수문 순서로, 레벨4는 만약·반복하기 배치로, 레벨5는 함수 안 에너지 블록 수로 채점한다.
    public static class BlockScorer
    {
        /// <summary>
        /// 프로그램 전체를 레벨 규칙에 맞게 채점해 총점을 반환한다 — 정답 방향은 출제 때 고른 문제 값의 정답(LevelData)을 받는다.
        /// </summary>
        public static int ScoreProgram(List<BlockInstruction> instructions, string questionValueKey, string correctAnswer,
            LevelKind kind, ILogger logger = null)
        {
            // 레벨 데이터와 블록 라벨이 어긋나 값을 읽지 못하면 logger(없으면 Unity 콘솔)에 경고를 남긴다.
            // 레벨4(발전소)는 명령에 값이 없어 아래 순회 채점과 무관 — 놀이시설/조건/병원 3항목을 더한 별도 채점
            if (kind == LevelKind.PowerPlant)
                return ScorePowerPlant(instructions);

            // 레벨5(미래에너지)는 함수 안에 넣은 에너지 블록 수로 채점
            if (kind == LevelKind.FutureEnergy)
                return GetEnergiesInFunction(instructions).Count * Constants.Scores.FutureEnergyBlockScore;

            int total = 0;
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                if (instr is CommandInstruction cmd)
                    total += ScoreCommand(cmd, correctAnswer, kind, logger);
            }

            // 레벨3(수력)은 결과 화면의 항목들처럼 첫 '만약' 블록 하나만 채점한다
            if (kind == LevelKind.Hydro)
                total += ScoreHydroIf(FindFirst<IfInstruction>(instructions), questionValueKey, logger);

            return total;
        }

        /// <summary>
        /// 수력 레벨의 '만약' 블록을 조건×아니면×수문 순서 3항목을 곱해 채점한다 (만약이 없으면 0점).
        /// </summary>
        private static int ScoreHydroIf(IfInstruction ifInstr, string questionValueKey, ILogger logger)
        {
            if (ifInstr is null) return 0;

            int conditionScore = ScoreHydroCondition(ifInstr.Condition, questionValueKey, logger);
            int elseScore = ifInstr.HasElseMarker
                ? Constants.Scores.HydroElsePlacedScore
                : Constants.Scores.HydroElseMissingScore;
            return conditionScore * elseScore * ScoreHydroGateOrder(ifInstr);
        }

        // ── 최고 점수 값 조회 (AI 코딩 결과 표시용) ─────────────────

        /// <summary>
        /// 레벨별 최고 점수를 반환한다.
        /// </summary>
        public static int GetMaxScore(LevelKind kind)
        {
            // 레벨3(수력)은 조건×아니면×순서 3항목을 곱한 점수 체계라 별도 계산식을 쓴다.
            if (kind == LevelKind.Hydro)
                return Constants.Scores.HydroExactScore
                     * Constants.Scores.HydroElsePlacedScore
                     * Constants.Scores.HydroGateOrderCorrectScore;

            // 풍력은 방향 3단계 채점(같은 방향=최고점)뿐이라 개수 점수를 더하지 않는다
            if (kind == LevelKind.Wind)
                return Constants.Scores.WindDirectionSameScore;

            if (kind == LevelKind.FutureEnergy)
                return Constants.BlockLabels.FutureEnergies.Count * Constants.Scores.FutureEnergyBlockScore;

            if (kind == LevelKind.PowerPlant)
                return Constants.Scores.PowerPlantAmusementOffScore
                     + Constants.Scores.PowerPlantConditionAndScore
                     + Constants.Scores.PowerPlantHospitalKeptScore;

            return Constants.Scores.DirectionCorrectScore + Constants.Scores.CountScore[GetBestCount()];
        }

        /// <summary>
        /// 가장 높은 점수를 주는 개수 값을 반환한다.
        /// </summary>
        public static string GetBestCount() => MaxScoreKey(Constants.Scores.CountScore);

        /// <summary>
        /// 프로그램에 반복하기 블록이 있는지 확인한다 — 레벨2 결과의 '반복 감지' 표시에 사용 (채점에는 반영되지 않음).
        /// </summary>
        public static bool ContainsRepeat(List<BlockInstruction> instructions)
            => ContainsType<RepeatInstruction>(instructions);

        /// <summary>
        /// 첫 '만약' 블록에 연결된 조건 높이를 "5m" 형태로 반환한다 — 레벨3 결과의 '수문 개방 높이'/'조건 감지' 표시용.
        /// </summary>
        public static string GetHydroGateHeight(List<BlockInstruction> instructions)
        {
            // 조건 블록이 없거나 높이를 읽을 수 없으면 null (= 조건 감지 OFF).
            IfInstruction ifInstr = FindFirst<IfInstruction>(instructions);
            if (ifInstr?.Condition is not SimpleConditionExpr simple) return null;

            int meters = ParseMeters(simple.Name);
            return meters >= 0 ? ZString.Concat(meters, "m") : null;
        }

        /// <summary>
        /// 첫 '만약' 블록 안에 '아니면'이 놓였는지 확인한다 — 레벨3 결과의 '수문 닫기 조건(아니면)' 표시용.
        /// </summary>
        public static bool HasHydroElse(List<BlockInstruction> instructions)
            => FindFirst<IfInstruction>(instructions)?.HasElseMarker ?? false;

        /// <summary>
        /// 첫 '만약' 블록의 수문 열기·닫기 순서가 맞는지 확인한다 — 레벨3 순서 채점과 결과의 '수문 열기·닫기 순서'가 같은 기준을 쓴다.
        /// </summary>
        public static bool IsHydroGateOrderCorrect(List<BlockInstruction> instructions)
        {
            IfInstruction ifInstr = FindFirst<IfInstruction>(instructions);
            return ifInstr is not null && IsHydroGateOrderCorrect(ifInstr);
        }

        /// <summary>
        /// 실행될 수 있는 첫 '만약' 블록의 조건식을 레벨4 결과의 '설정한 조건' 문구로 반환한다(없거나 무한 반복 뒤라 실행되지 않으면 null).
        /// </summary>
        public static string GetConditionText(List<BlockInstruction> instructions)
        {
            // 결과 텍스트는 폭이 좁아 '그리고'를 가운뎃점으로 줄인다 (디버그 코드 표시는 원문 그대로)
            IfInstruction ifInstr = FindReachableFirstIf(instructions);
            return ifInstr?.Condition switch
            {
                SimpleConditionExpr s => s.Name,
                LogicConditionExpr l when l.Operator == Constants.BlockLabels.And
                    => ZString.Concat(l.Left?.Name, " ", Constants.ResultMessages.ConditionAndSeparator, " ", l.Right?.Name),
                LogicConditionExpr l => ZString.Concat(l.Left?.Name, " ", l.Operator, " ", l.Right?.Name),
                _ => null
            };
        }

        /// <summary>
        /// '놀이시설 불 끄기'가 첫 '만약' 안(중첩 포함)에서 실제로 실행될 수 있고 함정 '놀이시설 불 켜기'를 쓰지 않았는지 확인한다 —
        /// 레벨4 놀이시설 채점과 결과의 '놀이시설 끄기 조건(만약)'이 같은 기준을 쓴다.
        /// </summary>
        public static bool IsAmusementPowerCut(List<BlockInstruction> instructions)
        {
            // 무한 반복하기 뒤에 놓여 실행되지 않는 블록은 인정하지 않는다.
            IfInstruction ifInstr = FindFirst<IfInstruction>(instructions);
            if (ifInstr is null || ContainsCommandDeep(instructions, Constants.BlockLabels.AmusementOn)) return false;

            HashSet<BlockInstruction> reachable = new HashSet<BlockInstruction>();
            CollectReachable(instructions, reachable);
            foreach (BlockInstruction instr in InstructionTree.Traverse(ifInstr.Then))
                if (instr is CommandInstruction cmd && cmd.Command == Constants.BlockLabels.AmusementOff && reachable.Contains(cmd))
                    return true;
            return false;
        }

        /// <summary>
        /// 첫 '만약'이 실행될 수 있으면 돌려주고, 없거나 무한 반복하기 뒤에 놓여 실행되지 않으면 null을 돌려준다.
        /// </summary>
        private static IfInstruction FindReachableFirstIf(List<BlockInstruction> instructions)
        {
            // 레벨4 조건 채점과 결과의 '설정한 조건'이 같은 기준을 쓴다 — 실행되지 않는 만약의 조건은 인정하지 않는다
            IfInstruction ifInstr = FindFirst<IfInstruction>(instructions);
            if (ifInstr is null) return null;

            HashSet<BlockInstruction> reachable = new HashSet<BlockInstruction>();
            CollectReachable(instructions, reachable);
            return reachable.Contains(ifInstr) ? ifInstr : null;
        }

        /// <summary>
        /// 실행 순서대로 도달할 수 있는 명령을 모으고, 이 목록의 실행이 끝나지 않으면(무한 반복에 갇히면) true를 반환한다.
        /// </summary>
        private static bool CollectReachable(List<BlockInstruction> list, HashSet<BlockInstruction> reachable)
        {
            // 무한 반복하기는 끝나지 않으므로 그 뒤 블록은 같은 목록이든 바깥 목록이든 실행되지 않는다.
            if (list is null) return false;

            foreach (BlockInstruction instr in list)
            {
                reachable.Add(instr);
                switch (instr)
                {
                    case RepeatInstruction rep:
                        bool bodyNeverEnds = CollectReachable(rep.Body, reachable);
                        if (rep.IsInfinite || bodyNeverEnds) return true;
                        break;
                    case IfInstruction ifInstr:
                        bool thenNeverEnds = CollectReachable(ifInstr.Then, reachable);
                        bool elseNeverEnds = CollectReachable(ifInstr.Else, reachable);
                        // 아니면이 없으면 조건이 거짓일 때 그냥 지나가므로, 두 분기가 모두 끝나지 않을 때만 뒤 블록이 막힌다
                        if (ifInstr.HasElseMarker && thenNeverEnds && elseNeverEnds) return true;
                        break;
                    case FunctionInstruction fn:
                        if (CollectReachable(fn.Body, reachable)) return true;
                        break;
                }
            }
            return false;
        }

        /// <summary>
        /// '병원 불 켜기'가 반복하기 안에 있고 함정 '병원 불 끄기'를 쓰지 않았는지 확인한다 —
        /// 레벨4 병원 채점과 결과의 '병원 전력 유지'가 같은 기준을 쓴다.
        /// </summary>
        public static bool IsHospitalPowerKept(List<BlockInstruction> instructions)
        {
            // 반복하기가 만약 안이든 밖이든 상관없다.
            RepeatInstruction repInstr = FindFirst<RepeatInstruction>(instructions);
            return repInstr is not null
                && ContainsCommandDeep(repInstr.Body, Constants.BlockLabels.HospitalOn)
                && !ContainsCommandDeep(instructions, Constants.BlockLabels.HospitalOff);
        }

        /// <summary>
        /// 프로그램에서 함수를 호출했는지 확인한다 — 레벨5 결과의 '함수 사용' 표시용.
        /// </summary>
        public static bool UsesFunction(List<BlockInstruction> instructions)
            => ContainsType<FunctionInstruction>(instructions);

        /// <summary>
        /// 호출한 함수 안(함수 정의 블록 안)에 들어 있는 레벨5 에너지 블록 이름을 모은다 — 레벨5 채점과 결과의 에너지별 ON/OFF가 같은 기준을 쓴다.
        /// </summary>
        public static List<string> GetEnergiesInFunction(List<BlockInstruction> instructions)
        {
            // 함수 본문들을 한 번만 훑어 명령 이름을 모은 뒤, 결과 화면 순서(에너지 목록 순서)대로 골라낸다
            HashSet<string> commandsInFunction = new HashSet<string>();
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                if (instr is not FunctionInstruction fn) continue;

                foreach (BlockInstruction inner in InstructionTree.Traverse(fn.Body))
                    if (inner is CommandInstruction cmd) commandsInFunction.Add(cmd.Command);
            }

            List<string> found = new List<string>();
            foreach (string energy in Constants.BlockLabels.FutureEnergies)
                if (commandsInFunction.Contains(energy)) found.Add(energy);
            return found;
        }

        /// <summary>
        /// 점수표에서 가장 높은 점수의 키를 반환한다.
        /// </summary>
        private static string MaxScoreKey(IReadOnlyDictionary<string, int> table)
        {
            string bestKey = null;
            int bestScore = int.MinValue;
            foreach (KeyValuePair<string, int> pair in table)
                if (pair.Value > bestScore)
                {
                    bestScore = pair.Value;
                    bestKey = pair.Key;
                }
            return bestKey;
        }

        // ── 프로그램에서 플레이어가 조립한 값 추출 ──────────────────

        /// <summary>
        /// 프로그램에서 플레이어가 조립한 방향·개수 값을 추출한다 (같은 타입의 Command가 여러 개면 마지막 값).
        /// </summary>
        public static (string direction, string count) ExtractValues(List<BlockInstruction> instructions)
        {
            string direction = null, count = null;

            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
            {
                if (instr is not CommandInstruction cmd || cmd.Value is null) continue;

                switch (cmd.ValueKind)
                {
                    case ValueKind.Direction: direction = cmd.Value; break;
                    case ValueKind.Count:     count     = cmd.Value; break;
                }
            }

            return (direction, count);
        }

        /// <summary>
        /// Command 하나를 연결된 값의 종류에 맞게 채점한다.
        /// </summary>
        public static int ScoreCommand(CommandInstruction cmd, string correctAnswer, LevelKind kind, ILogger logger = null)
        {
            if (cmd.Value is null) return 0;

            switch (cmd.ValueKind)
            {
                case ValueKind.Direction:
                    if (kind == LevelKind.Wind)
                        return ScoreWindDirection(cmd.Value, correctAnswer);
                    return correctAnswer is not null && cmd.Value == correctAnswer
                        ? Constants.Scores.DirectionCorrectScore
                        : Constants.Scores.DirectionWrongScore;
                case ValueKind.Count:
                    return ScoreFromTable(Constants.Scores.CountScore, cmd.Value, logger);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 점수표에서 값 블록 라벨의 점수를 찾는다 — 표에 없는 라벨은 블록 데이터가 어긋난 것이라 경고하고 0점을 준다.
        /// </summary>
        private static int ScoreFromTable(IReadOnlyDictionary<string, int> table, string label, ILogger logger)
        {
            if (table.TryGetValue(label, out int score)) return score;

            LogWarning(logger, ZString.Concat("[BlockScorer] 점수표에 없는 값 블록 '", label, "'이라 0점으로 채점합니다. 블록 라벨과 Constants.Scores를 확인하세요."));
            return 0;
        }

        /// <summary>
        /// 풍력 레벨 방향을 채점한다 — 문제의 정답 방향과 같으면 3점, 정반대면 1점, 그 외는 2점.
        /// </summary>
        private static int ScoreWindDirection(string chosen, string correct)
        {
            if (correct is not null && chosen == correct)
                return Constants.Scores.WindDirectionSameScore;

            if (correct is not null && Constants.Directions.Opposite.TryGetValue(correct, out string opposite) && chosen == opposite)
                return Constants.Scores.WindDirectionOppositeScore;

            return Constants.Scores.WindDirectionOtherScore;
        }

        /// <summary>
        /// 수력 레벨 조건을 채점한다 — 만약 블록에 연결한 높이 조건("5m 이상")과 문제 높이("5m")를 비교해
        /// 정확히 같으면 5점, 더 낮게 연결했으면 3점, 더 높게 연결했으면 1점.
        /// </summary>
        private static int ScoreHydroCondition(ConditionExpr condition, string questionValueKey, ILogger logger)
        {
            if (condition is not SimpleConditionExpr simple) return 0;

            int chosen = ParseMeters(simple.Name);
            int target = ParseMeters(questionValueKey);
            if (chosen < 0 || target < 0)
            {
                LogWarning(logger, ZString.Concat("[BlockScorer] 높이를 읽지 못해 수력 조건을 0점으로 채점합니다 (조건 '", simple.Name, "', 문제 '", questionValueKey, "')."));
                return 0;
            }

            if (chosen == target) return Constants.Scores.HydroExactScore;
            return chosen < target ? Constants.Scores.HydroLowerScore : Constants.Scores.HydroHigherScore;
        }

        /// <summary>
        /// "5m 이상" / "5m" 등 앞자리 숫자를 미터 값으로 파싱한다 (실패 시 -1).
        /// </summary>
        private static int ParseMeters(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;

            int i = 0;
            while (i < text.Length && char.IsDigit(text[i])) i++;
            return i > 0 && int.TryParse(text.Substring(0, i), out int meters) ? meters : -1;
        }

        /// <summary>
        /// 레벨4를 채점한다 — 놀이시설(만약 안에서 끄기) + 조건(단일/그리고/또는) + 병원(반복하기 안에서 켜기) 3항목을 더한다.
        /// </summary>
        private static int ScorePowerPlant(List<BlockInstruction> instructions)
        {
            // 함정 블록(놀이시설 불 켜기·병원 불 끄기·낮·전기 여유)을 쓰면 관련 항목이 감점된다.
            // 무한 반복하기 뒤에 놓여 실행되지 않는 만약은 조건도 0점이다
            IfInstruction ifInstr = FindReachableFirstIf(instructions);

            int amusementScore = IsAmusementPowerCut(instructions)
                ? Constants.Scores.PowerPlantAmusementOffScore
                : Constants.Scores.PowerPlantAmusementOtherScore;

            int conditionScore = ifInstr is not null ? ScorePowerPlantCondition(ifInstr.Condition) : 0;

            int hospitalScore = IsHospitalPowerKept(instructions)
                ? Constants.Scores.PowerPlantHospitalKeptScore
                : Constants.Scores.PowerPlantHospitalOtherScore;

            return amusementScore + conditionScore + hospitalScore;
        }

        /// <summary>
        /// 발전소 조건을 채점한다 — 함정 조건(낮·전기 여유)이 하나라도 있으면 0점,
        /// 그 외 조건 블록 1개만 연결(단순 조건) 5점 / 그리고로 연결 10점 / 또는으로 연결 5점.
        /// </summary>
        private static int ScorePowerPlantCondition(ConditionExpr condition) => condition switch
        {
            _ when HasTrapCondition(condition) => Constants.Scores.PowerPlantConditionTrapScore,
            LogicConditionExpr logic when logic.Operator == Constants.BlockLabels.And => Constants.Scores.PowerPlantConditionAndScore,
            LogicConditionExpr => Constants.Scores.PowerPlantConditionOrScore,
            SimpleConditionExpr => Constants.Scores.PowerPlantConditionSingleScore,
            _ => 0
        };

        /// <summary>
        /// 조건식에 함정 조건 블록(낮·전기 여유)이 들어 있는지 확인한다.
        /// </summary>
        private static bool HasTrapCondition(ConditionExpr condition) => condition switch
        {
            SimpleConditionExpr s => IsTrapCondition(s.Name),
            LogicConditionExpr l => IsTrapCondition(l.Left?.Name) || IsTrapCondition(l.Right?.Name),
            _ => false
        };

        /// <summary>
        /// 조건 블록 이름이 함정 조건인지 확인한다.
        /// </summary>
        private static bool IsTrapCondition(string name)
            => name is Constants.BlockLabels.DayCondition or Constants.BlockLabels.SpareCondition;

        /// <summary>
        /// 전위 순회에서 처음 만나는 지정 타입 명령을 반환한다.
        /// </summary>
        private static T FindFirst<T>(List<BlockInstruction> instructions) where T : BlockInstruction
        {
            foreach (BlockInstruction instr in InstructionTree.Traverse(instructions))
                if (instr is T match) return match;
            return null;
        }

        /// <summary>
        /// 본문(중첩 포함)에 지정 타입 명령이 있는지 확인한다.
        /// </summary>
        private static bool ContainsType<T>(List<BlockInstruction> body) where T : BlockInstruction
        {
            foreach (BlockInstruction instr in InstructionTree.Traverse(body))
                if (instr is T) return true;
            return false;
        }

        /// <summary>
        /// 본문(중첩 포함)에 지정 이름의 Command가 있는지 확인한다.
        /// </summary>
        private static bool ContainsCommandDeep(List<BlockInstruction> body, string commandName)
        {
            foreach (BlockInstruction instr in InstructionTree.Traverse(body))
                if (instr is CommandInstruction cmd && cmd.Command == commandName)
                    return true;
            return false;
        }

        // 둘 다 한쪽에 몰려있거나 순서가 반대(수문 닫기가 Then, 수문 열기가 Else)면 1점.
        /// <summary>
        /// 수력 레벨 개방/폐쇄 순서를 채점한다 — 수문 열기가 Then에, 수문 닫기가 Else에 있어야 정답(5점).
        /// </summary>
        private static int ScoreHydroGateOrder(IfInstruction ifInstr)
            => IsHydroGateOrderCorrect(ifInstr)
                ? Constants.Scores.HydroGateOrderCorrectScore
                : Constants.Scores.HydroGateOrderWrongScore;

        /// <summary>
        /// 수문 열기가 Then 최상위에, 수문 닫기가 Else 최상위에 있는지 확인한다.
        /// </summary>
        private static bool IsHydroGateOrderCorrect(IfInstruction ifInstr)
            => ContainsCommand(ifInstr.Then, Constants.BlockLabels.HydroOpen)
            && ContainsCommand(ifInstr.Else, Constants.BlockLabels.HydroClose);

        /// <summary>
        /// 본문 최상위(중첩 제외)에 지정 이름의 Command가 있는지 확인한다.
        /// </summary>
        private static bool ContainsCommand(List<BlockInstruction> body, string commandName)
        {
            if (body is null) return false;
            foreach (BlockInstruction instr in body)
                if (instr is CommandInstruction cmd && cmd.Command == commandName)
                    return true;
            return false;
        }

        /// <summary>
        /// 로거가 있으면 ZLogger로, 없으면 Unity 콘솔로 경고를 남긴다 (정적 유틸리티라 로거를 선택 인자로 받음).
        /// </summary>
        private static void LogWarning(ILogger logger, string message)
        {
            if (logger != null) logger.ZLogWarning($"{message}");
            else Debug.LogWarning(message);
        }
    }
}
