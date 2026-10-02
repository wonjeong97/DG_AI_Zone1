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

            // 아니면은 넣었지만 비어 있음 — 아니면 ON, 순서만 오류
            Assert.IsTrue(BlockScorer.HasHydroElse(wrongs[3]));
            Assert.AreEqual(Constants.Scores.HydroExactScore * Constants.Scores.HydroElsePlacedScore * Constants.Scores.HydroGateOrderWrongScore,
                BlockScorer.ScoreProgram(wrongs[3], "5m", null, LevelKind.Hydro));

            Assert.IsFalse(BlockScorer.IsHydroGateOrderCorrect(new List<BlockInstruction>()), "만약 블록이 없으면 오류");
        }

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
