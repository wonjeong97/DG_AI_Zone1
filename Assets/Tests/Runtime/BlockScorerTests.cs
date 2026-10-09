using System.Collections.Generic;
using Data;
using Game;
using Game.Runtime;
using NUnit.Framework;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 레벨 판별을 LevelData.kind(LevelKind)로 바꾸고 문자열 조합을 ZString으로 바꾼 뒤에도
    /// 채점 규칙과 결과 표시 값이 그대로인지 검증한다.
    /// </summary>
    public class BlockScorerTests
    {
        /// <summary>
        /// 채점용 방향 명령 노드를 만든다.
        /// </summary>
        private static CommandInstruction DirectionCommand(string value) => new CommandInstruction
        {
            Command = "풍차의 날개 방향",
            Value = value,
            ValueKind = ValueKind.Direction
        };

        /// <summary>
        /// 풍력 레벨은 바람과 같은 방향 3점, 정반대 1점, 그 외 2점으로 채점된다.
        /// </summary>
        [Test]
        public void 풍력_방향은_같음_정반대_그외로_3단계_채점된다()
        {
            // 풍력은 바람이 불어오는 방향이 곧 정답 날개 방향이다(02_WindData)
            string correct = Constants.Directions.East;

            Assert.AreEqual(Constants.Scores.WindDirectionSameScore,
                BlockScorer.ScoreCommand(DirectionCommand(Constants.Directions.East), correct, LevelKind.Wind));
            Assert.AreEqual(Constants.Scores.WindDirectionOppositeScore,
                BlockScorer.ScoreCommand(DirectionCommand(Constants.Directions.West), correct, LevelKind.Wind));
            Assert.AreEqual(Constants.Scores.WindDirectionOtherScore,
                BlockScorer.ScoreCommand(DirectionCommand(Constants.Directions.North), correct, LevelKind.Wind));
        }

        /// <summary>
        /// 수력 레벨 조건 높이는 문제와 같으면 최고점, 낮으면 중간, 높으면 최저점이다.
        /// </summary>
        [Test]
        public void 수력_조건_높이는_문제_높이와의_비교로_채점된다()
        {
            Assert.AreEqual(Constants.Scores.HydroExactScore * Constants.Scores.HydroElseMissingScore * Constants.Scores.HydroGateOrderWrongScore,
                BlockScorer.ScoreProgram(IfProgram("5m 이상"), "5m", null, LevelKind.Hydro));
            Assert.AreEqual(Constants.Scores.HydroLowerScore * Constants.Scores.HydroElseMissingScore * Constants.Scores.HydroGateOrderWrongScore,
                BlockScorer.ScoreProgram(IfProgram("3m 이상"), "5m", null, LevelKind.Hydro));
            Assert.AreEqual(Constants.Scores.HydroHigherScore * Constants.Scores.HydroElseMissingScore * Constants.Scores.HydroGateOrderWrongScore,
                BlockScorer.ScoreProgram(IfProgram("8m 이상"), "5m", null, LevelKind.Hydro));
        }

        /// <summary>
        /// 수력은 결과 화면처럼 첫 만약만 채점한다 — 만약을 두 개 놓아도 최고점을 넘지 않는다.
        /// </summary>
        [Test]
        public void 수력은_첫_만약만_채점해_최고점을_넘지_않는다()
        {
            List<BlockInstruction> twoIfs = GateProgram(new[] { Constants.BlockLabels.HydroOpen }, true, new[] { Constants.BlockLabels.HydroClose });
            twoIfs.AddRange(GateProgram(new[] { Constants.BlockLabels.HydroOpen }, true, new[] { Constants.BlockLabels.HydroClose }));

            Assert.AreEqual(BlockScorer.GetMaxScore(LevelKind.Hydro), BlockScorer.ScoreProgram(twoIfs, "5m", null, LevelKind.Hydro));
        }

        /// <summary>
        /// 결과 화면의 수문 개방 높이는 조건 문구에서 숫자만 읽어 "5m" 형태로 표시된다.
        /// </summary>
        [Test]
        public void 수문_개방_높이는_미터_단위로_표시된다()
        {
            Assert.AreEqual("5m", BlockScorer.GetHydroGateHeight(IfProgram("5m 이상")));
            Assert.IsNull(BlockScorer.GetHydroGateHeight(new List<BlockInstruction>()), "만약 블록이 없으면 null");
        }

        /// <summary>
        /// 수문 열기가 만약 안, 수문 닫기가 아니면 안에 있을 때만 순서가 맞고, 결과 표시 값이 순서 채점과 일치한다.
        /// 아니면을 넣어 높이·조건·아니면이 모두 AI와 같아도 순서가 틀리면 결과에서 구분된다.
        /// </summary>
        [Test]
        public void 수문_열기_닫기_순서는_만약에_열기_아니면에_닫기일_때만_정상이다()
        {
            List<BlockInstruction> correct = GateProgram(new[] { "수문 열기" }, true, new[] { "수문 닫기" });
            Assert.IsTrue(BlockScorer.IsHydroGateOrderCorrect(correct));
            Assert.AreEqual(BlockScorer.GetMaxScore(LevelKind.Hydro),
                BlockScorer.ScoreProgram(correct, "5m", null, LevelKind.Hydro));

            List<BlockInstruction>[] wrongs =
            {
                GateProgram(new[] { "수문 열기" }, false, new string[0]),
                GateProgram(new[] { "수문 닫기" }, false, new string[0]),
                GateProgram(new[] { "수문 닫기", "수문 열기" }, false, new string[0]),
                GateProgram(new[] { "수문 닫기", "수문 열기" }, true, new string[0]),
                GateProgram(new[] { "수문 닫기" }, true, new[] { "수문 열기" }),
            };
            foreach (List<BlockInstruction> wrong in wrongs)
                Assert.IsFalse(BlockScorer.IsHydroGateOrderCorrect(wrong));

            // 열기·닫기를 반대로 넣음 — 높이·조건·아니면은 AI와 같고 순서만 오류
            Assert.IsTrue(BlockScorer.HasHydroElse(wrongs[4]));
            Assert.AreEqual(Constants.Scores.HydroExactScore * Constants.Scores.HydroElsePlacedScore * Constants.Scores.HydroGateOrderWrongScore,
                BlockScorer.ScoreProgram(wrongs[4], "5m", null, LevelKind.Hydro));

            Assert.IsFalse(BlockScorer.IsHydroGateOrderCorrect(new List<BlockInstruction>()), "만약 블록이 없으면 오류");
        }

        /// <summary>
        /// 레벨4는 병원 반복하기를 만약 안에 넣든 밖에 두든 만점이고, 함정 블록을 쓰거나 놀이시설 불 끄기를 만약 밖에 두면 관련 항목이 감점된다.
        /// </summary>
        [Test]
        public void 발전소는_반복하기_위치와_상관없이_만점이고_함정_블록은_감점된다()
        {
            int max = BlockScorer.GetMaxScore(LevelKind.PowerPlant);
            int and = Constants.Scores.PowerPlantConditionAndScore;

            List<BlockInstruction> nested = new List<BlockInstruction>
                { PlantIf(Cmd("놀이시설 불 끄기"), Repeat(Cmd("병원 불 켜기"))) };
            Assert.AreEqual(max, BlockScorer.ScoreProgram(nested, null, null, LevelKind.PowerPlant), "반복하기를 만약 안에 중첩");

            List<BlockInstruction> repeatOutside = new List<BlockInstruction>
                { PlantIf(Cmd("놀이시설 불 끄기")), Repeat(Cmd("병원 불 켜기")) };
            Assert.AreEqual(max, BlockScorer.ScoreProgram(repeatOutside, null, null, LevelKind.PowerPlant), "반복하기를 만약 밖에 둠");
            Assert.IsTrue(BlockScorer.IsAmusementPowerCut(repeatOutside));
            Assert.IsTrue(BlockScorer.IsHospitalPowerKept(repeatOutside));

            List<BlockInstruction> amusementOnTrap = new List<BlockInstruction>
                { PlantIf(Cmd("놀이시설 불 끄기")), Cmd("놀이시설 불 켜기"), Repeat(Cmd("병원 불 켜기")) };
            Assert.IsFalse(BlockScorer.IsAmusementPowerCut(amusementOnTrap));
            Assert.AreEqual(Constants.Scores.PowerPlantAmusementOtherScore + and + Constants.Scores.PowerPlantHospitalKeptScore,
                BlockScorer.ScoreProgram(amusementOnTrap, null, null, LevelKind.PowerPlant), "함정 놀이시설 불 켜기");

            List<BlockInstruction> hospitalOffTrap = new List<BlockInstruction>
                { PlantIf(Cmd("놀이시설 불 끄기")), Repeat(Cmd("병원 불 켜기"), Cmd("병원 불 끄기")) };
            Assert.IsFalse(BlockScorer.IsHospitalPowerKept(hospitalOffTrap));
            Assert.AreEqual(Constants.Scores.PowerPlantAmusementOffScore + and + Constants.Scores.PowerPlantHospitalOtherScore,
                BlockScorer.ScoreProgram(hospitalOffTrap, null, null, LevelKind.PowerPlant), "함정 병원 불 끄기");

            List<BlockInstruction> amusementOutsideIf = new List<BlockInstruction>
                { PlantIf(Repeat(Cmd("병원 불 켜기"))), Cmd("놀이시설 불 끄기") };
            Assert.IsFalse(BlockScorer.IsAmusementPowerCut(amusementOutsideIf), "놀이시설 불 끄기가 만약 밖");
        }

        /// <summary>
        /// 레벨5는 함수 안에 넣은 에너지 블록 수로 채점한다 — 4개면 만점, 3개면 75%, 함수 밖 명령은 세지 않는다.
        /// </summary>
        [Test]
        public void 미래에너지는_함수_안에_넣은_에너지_수로_채점된다()
        {
            int max = BlockScorer.GetMaxScore(LevelKind.FutureEnergy);

            List<BlockInstruction> allFour = new List<BlockInstruction>
                { Function(Cmd("태양광"), Cmd("풍력"), Cmd("수력 발전"), Cmd("스마트 도시 발전소")) };
            Assert.AreEqual(max, BlockScorer.ScoreProgram(allFour, null, null, LevelKind.FutureEnergy), "4개 모두");
            Assert.IsTrue(BlockScorer.UsesFunction(allFour));
            CollectionAssert.AreEqual(Constants.BlockLabels.FutureEnergies, BlockScorer.GetEnergiesInFunction(allFour));

            List<BlockInstruction> three = new List<BlockInstruction>
                { Function(Cmd("태양광"), Cmd("수력 발전"), Cmd("스마트 도시 발전소")) };
            Assert.AreEqual(75, BlockScorer.ScoreProgram(three, null, null, LevelKind.FutureEnergy) * 100 / max, "3개 → 75%");
            CollectionAssert.DoesNotContain(BlockScorer.GetEnergiesInFunction(three), "풍력");

            List<BlockInstruction> outsideFunction = new List<BlockInstruction> { Cmd("태양광"), Function(Cmd("풍력")) };
            CollectionAssert.AreEqual(new[] { "풍력" }, BlockScorer.GetEnergiesInFunction(outsideFunction), "함수 밖 태양광은 제외");

            List<BlockInstruction> none = new List<BlockInstruction>();
            Assert.AreEqual(0, BlockScorer.ScoreProgram(none, null, null, LevelKind.FutureEnergy));
            Assert.IsFalse(BlockScorer.UsesFunction(none));

            // 함수 호출 없이 시작하기 아래에만 에너지를 둔 경우 — 함수 정의 안에 넣어 둔 블록도 호출되지 않으면 프로그램에 없다
            List<BlockInstruction> mainChainOnly = new List<BlockInstruction> { Cmd("태양광"), Cmd("풍력") };
            Assert.AreEqual(0, BlockScorer.ScoreProgram(mainChainOnly, null, null, LevelKind.FutureEnergy), "함수 호출 없음");
            Assert.IsFalse(BlockScorer.UsesFunction(mainChainOnly));
        }

        /// <summary>
        /// 함수 정의 본문을 펼쳐 담은 함수 호출 노드를 만든다.
        /// </summary>
        private static FunctionInstruction Function(params BlockInstruction[] body) => new FunctionInstruction
        {
            Name = "미래 에너지 만들기",
            DefName = "미래 에너지 만들기",
            Body = new List<BlockInstruction>(body)
        };

        /// <summary>
        /// 무한 반복하기 뒤에 놓여 실행되지 않는 놀이시설 불 끄기는 인정하지 않고, 반복 안에서 상황을 확인하는 만약은 인정한다.
        /// </summary>
        [Test]
        public void 무한_반복_뒤에_있어_실행되지_않는_놀이시설_끄기는_인정하지_않는다()
        {
            List<BlockInstruction> ifAfterRepeat = new List<BlockInstruction>
                { Repeat(Cmd("병원 불 켜기")), PlantIf(Cmd("놀이시설 불 끄기")) };
            Assert.IsFalse(BlockScorer.IsAmusementPowerCut(ifAfterRepeat), "만약이 무한 반복 뒤");
            Assert.AreEqual(Constants.Scores.PowerPlantAmusementOtherScore + Constants.Scores.PowerPlantConditionAndScore + Constants.Scores.PowerPlantHospitalKeptScore,
                BlockScorer.ScoreProgram(ifAfterRepeat, null, null, LevelKind.PowerPlant));

            List<BlockInstruction> offAfterRepeatInIf = new List<BlockInstruction>
                { PlantIf(Repeat(Cmd("병원 불 켜기")), Cmd("놀이시설 불 끄기")) };
            Assert.IsFalse(BlockScorer.IsAmusementPowerCut(offAfterRepeatInIf), "만약 안에서 무한 반복 뒤");

            List<BlockInstruction> ifInsideRepeat = new List<BlockInstruction>
                { Repeat(PlantIf(Cmd("놀이시설 불 끄기")), Cmd("병원 불 켜기")) };
            Assert.IsTrue(BlockScorer.IsAmusementPowerCut(ifInsideRepeat), "반복 안에서 상황 확인");
            Assert.AreEqual(BlockScorer.GetMaxScore(LevelKind.PowerPlant),
                BlockScorer.ScoreProgram(ifInsideRepeat, null, null, LevelKind.PowerPlant));

            // 반복하기(무한) { 놀이시설 불 끄기 } → 만약(전기 과부하 또는 낮) { 놀이시설 불 켜기 }
            List<BlockInstruction> alwaysOff = new List<BlockInstruction>
                { Repeat(Cmd("놀이시설 불 끄기")), PlantIfWith(Logic("전기 과부하", "또는", "낮"), Cmd("놀이시설 불 켜기")) };
            Assert.IsFalse(BlockScorer.IsAmusementPowerCut(alwaysOff), "항상 끄기는 만약 안 끄기가 아님");
            Assert.AreEqual(Constants.Scores.PowerPlantAmusementOtherScore + Constants.Scores.PowerPlantConditionTrapScore + Constants.Scores.PowerPlantHospitalOtherScore,
                BlockScorer.ScoreProgram(alwaysOff, null, null, LevelKind.PowerPlant));
        }

        /// <summary>
        /// 레벨4 조건은 그리고 10점 / 또는·단일 5점이고, 함정 조건(낮·전기 여유)이 하나라도 들어가면 0점이다.
        /// </summary>
        [Test]
        public void 발전소_조건에_함정_조건이_들어가면_0점이다()
        {
            Assert.AreEqual(Constants.Scores.PowerPlantConditionAndScore, PlantConditionScore(Logic("전기 과부하", "그리고", "밤")));
            Assert.AreEqual(Constants.Scores.PowerPlantConditionOrScore, PlantConditionScore(Logic("전기 과부하", "또는", "밤")));
            Assert.AreEqual(Constants.Scores.PowerPlantConditionSingleScore, PlantConditionScore(new SimpleConditionExpr { Name = "밤" }));

            Assert.AreEqual(Constants.Scores.PowerPlantConditionTrapScore, PlantConditionScore(Logic("전기 과부하", "그리고", "낮")), "그리고 + 낮");
            Assert.AreEqual(Constants.Scores.PowerPlantConditionTrapScore, PlantConditionScore(Logic("전기 여유", "또는", "밤")), "또는 + 전기 여유");
            Assert.AreEqual(Constants.Scores.PowerPlantConditionTrapScore, PlantConditionScore(new SimpleConditionExpr { Name = "낮" }), "낮 단독");
        }

        /// <summary>
        /// 놀이시설·병원 항목을 만점으로 두고 조건만 바꾼 프로그램의 조건 점수를 구한다.
        /// </summary>
        private static int PlantConditionScore(ConditionExpr condition)
        {
            List<BlockInstruction> program = new List<BlockInstruction>
                { PlantIfWith(condition, Cmd("놀이시설 불 끄기")), Repeat(Cmd("병원 불 켜기")) };
            return BlockScorer.ScoreProgram(program, null, null, LevelKind.PowerPlant)
                 - Constants.Scores.PowerPlantAmusementOffScore
                 - Constants.Scores.PowerPlantHospitalKeptScore;
        }

        /// <summary>
        /// 두 조건을 논리 블록으로 이은 조건식을 만든다.
        /// </summary>
        private static LogicConditionExpr Logic(string left, string op, string right) => new LogicConditionExpr
        {
            Operator = op,
            Left = new SimpleConditionExpr { Name = left },
            Right = new SimpleConditionExpr { Name = right }
        };

        /// <summary>
        /// '전기 과부하 그리고 밤' 조건의 만약 블록을 만든다.
        /// </summary>
        private static IfInstruction PlantIf(params BlockInstruction[] then)
            => PlantIfWith(Logic("전기 과부하", "그리고", "밤"), then);

        /// <summary>
        /// 지정 조건의 만약 블록을 만든다.
        /// </summary>
        private static IfInstruction PlantIfWith(ConditionExpr condition, params BlockInstruction[] then) => new IfInstruction
        {
            Condition = condition,
            Then = new List<BlockInstruction>(then),
            Else = new List<BlockInstruction>()
        };

        /// <summary>
        /// 무한 반복하기 블록을 만든다.
        /// </summary>
        private static RepeatInstruction Repeat(params BlockInstruction[] body) => new RepeatInstruction
        {
            Count = -1,
            Body = new List<BlockInstruction>(body)
        };

        /// <summary>
        /// 값 없는 명령 노드를 만든다.
        /// </summary>
        private static CommandInstruction Cmd(string name) => new CommandInstruction { Command = name };

        /// <summary>
        /// "5m 이상" 조건의 만약 블록에 지정한 수문 명령을 넣은 프로그램을 만든다.
        /// </summary>
        private static List<BlockInstruction> GateProgram(string[] thenCommands, bool hasElse, string[] elseCommands) => new List<BlockInstruction>
        {
            new IfInstruction
            {
                Condition = new SimpleConditionExpr { Name = "5m 이상" },
                Then = Commands(thenCommands),
                Else = Commands(elseCommands),
                HasElseMarker = hasElse
            }
        };

        /// <summary>
        /// 값 없는 명령 노드 목록을 만든다.
        /// </summary>
        private static List<BlockInstruction> Commands(string[] names)
        {
            List<BlockInstruction> list = new List<BlockInstruction>();
            foreach (string name in names)
                list.Add(new CommandInstruction { Command = name });
            return list;
        }

        /// <summary>
        /// 단순 조건 하나만 가진 만약 블록 프로그램을 만든다.
        /// </summary>
        private static List<BlockInstruction> IfProgram(string conditionName) => new List<BlockInstruction>
        {
            new IfInstruction
            {
                Condition = new SimpleConditionExpr { Name = conditionName },
                Then = new List<BlockInstruction>(),
                Else = new List<BlockInstruction>()
            }
        };
    }
}
