using System.Collections.Generic;
using Data;
using NUnit.Framework;
using Scenes;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 결과 화면의 "항목 | 값" 행이 코딩 완료·건너뛰기 경우마다 기획대로 채워지는지 검증한다.
    /// 넘어가기와 정상 완료가 같은 행 생성 코드를 쓰도록 합친 뒤에도 건너뛴 경우의 '-' 표시가 그대로인지 본다.
    /// </summary>
    public class ResultRowFactoryTests
    {
        /// <summary>
        /// 코딩을 건너뛴 수력은 수문 개방 높이와 수문 순서가 '-', 조건 감지와 아니면은 OFF다.
        /// </summary>
        [Test]
        public void 건너뛴_수력은_높이와_순서가_비어_있고_감지는_꺼져_있다()
        {
            GameSession session = new GameSession { lastQuestionTime = "5m" };

            List<ResultRow> rows = ResultRowFactory.BuildPlayerRows(LevelKind.Hydro, session, false, Constants.ResultMessages.StatusPoor);

            Assert.AreEqual(Constants.ResultMessages.NoValue, ValueOf(rows, Constants.ResultMessages.LabelGateHeight));
            Assert.AreEqual(Constants.ResultMessages.DetectedOff, ValueOf(rows, Constants.ResultMessages.LabelConditionOn));
            Assert.AreEqual(Constants.ResultMessages.DetectedOff, ValueOf(rows, Constants.ResultMessages.LabelElse));
            Assert.AreEqual(Constants.ResultMessages.NoValue, ValueOf(rows, Constants.ResultMessages.LabelGateOrder));
        }

        /// <summary>
        /// 코딩을 건너뛴 발전소는 상황은 문제 그대로, 놀이시설·병원 항목은 판단할 배치가 없어 '-'다.
        /// </summary>
        [Test]
        public void 건너뛴_발전소는_놀이시설과_병원_항목이_비어_있다()
        {
            GameSession session = new GameSession();

            List<ResultRow> rows = ResultRowFactory.BuildPlayerRows(LevelKind.PowerPlant, session, false, Constants.ResultMessages.StatusPoor);

            Assert.AreEqual(Constants.ResultMessages.PowerPlantSituation, ValueOf(rows, Constants.ResultMessages.LabelSituation));
            Assert.AreEqual(Constants.ResultMessages.NoValue, ValueOf(rows, Constants.ResultMessages.LabelAmusement));
            Assert.AreEqual(Constants.ResultMessages.NoValue, ValueOf(rows, Constants.ResultMessages.LabelHospital));
        }

        /// <summary>
        /// 코딩을 완료한 발전소는 놀이시설·병원 항목을 ON/OFF로 보여 준다.
        /// </summary>
        [Test]
        public void 완료한_발전소는_놀이시설과_병원_항목을_켜짐_꺼짐으로_보여_준다()
        {
            GameSession session = new GameSession
            {
                lastConditionText = "전기 과부하 · 밤",
                lastAmusementPowerCut = true,
                lastHospitalPowerKept = false
            };

            List<ResultRow> rows = ResultRowFactory.BuildPlayerRows(LevelKind.PowerPlant, session, true, Constants.ResultMessages.StatusNormal);

            Assert.AreEqual("전기 과부하 · 밤", ValueOf(rows, Constants.ResultMessages.LabelCondition));
            Assert.AreEqual(Constants.ResultMessages.DetectedOn, ValueOf(rows, Constants.ResultMessages.LabelAmusement));
            Assert.AreEqual(Constants.ResultMessages.DetectedOff, ValueOf(rows, Constants.ResultMessages.LabelHospital));
        }

        /// <summary>
        /// 코딩을 건너뛴 풍력도 문제로 나온 바람 방향은 보여 주고, 체험자가 정한 풍차 방향만 '-'다.
        /// </summary>
        [Test]
        public void 건너뛴_풍력은_문제_바람_방향을_그대로_보여_준다()
        {
            GameSession session = new GameSession { lastQuestionTime = Constants.Directions.North, lastDirection = Constants.Directions.South };

            List<ResultRow> rows = ResultRowFactory.BuildPlayerRows(LevelKind.Wind, session, false, Constants.ResultMessages.StatusPoor);

            Assert.AreEqual(Constants.Directions.North, ValueOf(rows, Constants.ResultMessages.LabelWindDirection));
            Assert.AreEqual(Constants.ResultMessages.NoValue, ValueOf(rows, Constants.ResultMessages.LabelBladeDirection));
        }

        /// <summary>
        /// AI 미래에너지 결과는 모든 에너지 블록이 ON이다.
        /// </summary>
        [Test]
        public void AI_미래에너지_결과는_모든_에너지가_켜져_있다()
        {
            List<ResultRow> rows = ResultRowFactory.BuildAiRows(LevelKind.FutureEnergy, null, null);

            foreach (string energy in Constants.BlockLabels.FutureEnergies)
                Assert.AreEqual(Constants.ResultMessages.DetectedOn, ValueOf(rows, energy), $"{energy}가 꺼져 있음");
        }

        /// <summary>
        /// 전력 수급 상태는 보통 기준 미만이면 부족, 양호 기준 미만이면 보통, 그 이상이면 양호다.
        /// </summary>
        [Test]
        public void 전력_수급_상태는_비율_경계로_나뉜다()
        {
            Assert.AreEqual(Constants.ResultMessages.StatusPoor, ResultRowFactory.ToStatusText(49.9f));
            Assert.AreEqual(Constants.ResultMessages.StatusNormal, ResultRowFactory.ToStatusText(50f));
            Assert.AreEqual(Constants.ResultMessages.StatusNormal, ResultRowFactory.ToStatusText(74.9f));
            Assert.AreEqual(Constants.ResultMessages.StatusGood, ResultRowFactory.ToStatusText(75f));
        }

        /// <summary>
        /// 행 목록에서 이름이 label인 행의 값을 찾는다.
        /// </summary>
        private static string ValueOf(List<ResultRow> rows, string label)
        {
            foreach (ResultRow row in rows)
                if (row.Label == label) return row.Value;

            Assert.Fail($"'{label}' 행이 없음");
            return null;
        }
    }
}
