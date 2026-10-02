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
            string wind = Constants.Directions.East;

            Assert.AreEqual(Constants.Scores.WindDirectionSameScore,
                BlockScorer.ScoreCommand(DirectionCommand(Constants.Directions.East), wind, LevelKind.Wind));
            Assert.AreEqual(Constants.Scores.WindDirectionOppositeScore,
                BlockScorer.ScoreCommand(DirectionCommand(Constants.Directions.West), wind, LevelKind.Wind));
            Assert.AreEqual(Constants.Scores.WindDirectionOtherScore,
                BlockScorer.ScoreCommand(DirectionCommand(Constants.Directions.North), wind, LevelKind.Wind));
        }

        /// <summary>
        /// 수력 레벨 조건 높이는 문제와 같으면 최고점, 낮으면 중간, 높으면 최저점이다.
        /// </summary>
        [Test]
        public void 수력_조건_높이는_문제_높이와의_비교로_채점된다()
        {
            Assert.AreEqual(Constants.Scores.HydroExactScore * Constants.Scores.HydroElseMissingScore * Constants.Scores.HydroGateOrderWrongScore,
                BlockScorer.ScoreProgram(IfProgram("5m 이상"), "5m", LevelKind.Hydro));
            Assert.AreEqual(Constants.Scores.HydroLowerScore * Constants.Scores.HydroElseMissingScore * Constants.Scores.HydroGateOrderWrongScore,
                BlockScorer.ScoreProgram(IfProgram("3m 이상"), "5m", LevelKind.Hydro));
            Assert.AreEqual(Constants.Scores.HydroHigherScore * Constants.Scores.HydroElseMissingScore * Constants.Scores.HydroGateOrderWrongScore,
                BlockScorer.ScoreProgram(IfProgram("8m 이상"), "5m", LevelKind.Hydro));
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
