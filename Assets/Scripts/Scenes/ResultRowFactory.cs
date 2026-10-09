using System.Collections.Generic;
using Data;
using Game.Runtime;

namespace Scenes
{
    // 결과 화면의 "항목 | 값" 행 목록을 레벨별로 만든다 — 세션 값과 문제 값만으로 정해지는 순수 함수 모음.
    public static class ResultRowFactory
    {
        /// <summary>
        /// 최고 점수 대비 비율(%)을 전력 수급 상태 문구로 변환한다.
        /// </summary>
        public static string ToStatusText(float percent) =>
            percent < Constants.ResultMessages.NormalThresholdPercent ? Constants.ResultMessages.StatusPoor :
            percent < Constants.ResultMessages.GoodThresholdPercent   ? Constants.ResultMessages.StatusNormal :
                                                                       Constants.ResultMessages.StatusGood;

        /// <summary>
        /// 레벨에 맞는 체험자 결과 행을 세션 값으로 만든다.
        /// 코딩을 건너뛰었으면 체험자가 정한 값은 '-', 감지 항목은 OFF, 판단할 배치가 없는 채점 항목은 '-'로 두고
        /// 문제로 주어진 값(바람 방향·발전소 상황)은 그대로 보여 준다.
        /// </summary>
        public static List<ResultRow> BuildPlayerRows(LevelKind kind, GameSession session, bool hasCoding, string status)
        {
            switch (kind)
            {
                case LevelKind.Wind:
                    return BuildWindRows(session.lastQuestionTime,
                        hasCoding ? session.lastDirection : null, hasCoding && session.lastRepeatUsed, status);

                case LevelKind.Hydro:
                    return BuildHydroRows(hasCoding ? session.lastGateHeight : null, hasCoding && session.lastElseUsed,
                        hasCoding ? GateOrderText(session.lastGateOrderCorrect) : Constants.ResultMessages.NoValue, status);

                case LevelKind.PowerPlant:
                    return BuildPowerPlantRows(hasCoding ? session.lastConditionText : null,
                        hasCoding ? OnOff(session.lastAmusementPowerCut) : Constants.ResultMessages.NoValue,
                        hasCoding ? OnOff(session.lastHospitalPowerKept) : Constants.ResultMessages.NoValue, status);

                case LevelKind.FutureEnergy:
                    return BuildFutureEnergyRows(hasCoding && session.lastFunctionUsed,
                        hasCoding ? session.lastEnergiesInFunction : null, status);

                default:
                    return BuildSolarRows(hasCoding ? session.lastCount : null, hasCoding ? session.lastDirection : null, status);
            }
        }

        /// <summary>
        /// AI 결과 행을 만든다 — AI는 항상 정답(풍력은 정답 방향, 수력은 문제와 같은 높이)이다.
        /// </summary>
        public static List<ResultRow> BuildAiRows(LevelKind kind, string questionValue, string correctAnswer)
        {
            string good = Constants.ResultMessages.StatusGood;

            switch (kind)
            {
                case LevelKind.Wind:
                    return BuildWindRows(questionValue, correctAnswer, true, good);
                case LevelKind.Hydro:
                    return BuildHydroRows(questionValue, true, GateOrderText(true), good);
                case LevelKind.PowerPlant:
                    return BuildPowerPlantRows(Constants.ResultMessages.PowerPlantBestCondition, OnOff(true), OnOff(true), good);
                case LevelKind.FutureEnergy:
                    return BuildFutureEnergyRows(true, Constants.BlockLabels.FutureEnergies, good);
                default:
                    return BuildSolarRows(BlockScorer.GetBestCount(), correctAnswer, good);
            }
        }

        /// <summary>
        /// 값이 비었으면 '-'로 바꾼다 (코딩을 건너뛰었거나 값을 읽지 못한 경우).
        /// </summary>
        private static string OrNoValue(string value)
            => string.IsNullOrEmpty(value) ? Constants.ResultMessages.NoValue : value;

        /// <summary>
        /// 감지 여부를 ON/OFF 표기로 바꾼다.
        /// </summary>
        private static string OnOff(bool isOn)
            => isOn ? Constants.ResultMessages.DetectedOn : Constants.ResultMessages.DetectedOff;

        /// <summary>
        /// 수문 열기·닫기 순서가 맞았는지를 정상/오류 표기로 바꾼다.
        /// </summary>
        private static string GateOrderText(bool isCorrect)
            => isCorrect ? Constants.ResultMessages.GateOrderCorrect : Constants.ResultMessages.GateOrderWrong;

        /// <summary>
        /// 레벨1(태양광) 결과 행 — 설치 개수 / 패널 방향 / 전력 수급 상태.
        /// </summary>
        private static List<ResultRow> BuildSolarRows(string count, string direction, string status) => new()
        {
            new ResultRow(Constants.ResultMessages.LabelCount, OrNoValue(count)),
            new ResultRow(Constants.ResultMessages.LabelDirection, OrNoValue(direction)),
            new ResultRow(Constants.ResultMessages.LabelStatus, status),
        };

        /// <summary>
        /// 레벨2(풍력) 결과 행 — 문제로 나온 바람 방향 / 체험자가 맞춘 풍차 방향 / 반복하기 사용 여부.
        /// 반복하기 없이는 컴파일이 막히므로 정상 플레이에서 반복 감지는 항상 ON이다.
        /// </summary>
        private static List<ResultRow> BuildWindRows(string windDirection, string bladeDirection, bool repeatUsed, string status) => new()
        {
            new ResultRow(Constants.ResultMessages.LabelWindDirection, OrNoValue(windDirection)),
            new ResultRow(Constants.ResultMessages.LabelBladeDirection, OrNoValue(bladeDirection)),
            new ResultRow(Constants.ResultMessages.LabelRepeat, OnOff(repeatUsed)),
            new ResultRow(Constants.ResultMessages.LabelStatus, status),
        };

        /// <summary>
        /// 레벨3(수력) 결과 행 — 체험자가 만약 블록에 연결한 수문 개방 높이 / 조건·아니면 사용 여부 / 수문 열기·닫기 순서.
        /// 조건 감지는 조건 블록 연결 여부 — 조건 없이는 컴파일이 막히므로 정상 플레이에선 항상 ON이다.
        /// 아니면과 수문 열기·닫기 순서는 채점 항목이라 틀렸을 때 AI 결과와 달라 보이도록 따로 표시한다.
        /// </summary>
        private static List<ResultRow> BuildHydroRows(string gateHeight, bool elseUsed, string gateOrder, string status) => new()
        {
            new ResultRow(Constants.ResultMessages.LabelGateHeight, OrNoValue(gateHeight)),
            new ResultRow(Constants.ResultMessages.LabelConditionOn, OnOff(!string.IsNullOrEmpty(gateHeight))),
            new ResultRow(Constants.ResultMessages.LabelElse, OnOff(elseUsed)),
            new ResultRow(Constants.ResultMessages.LabelGateOrder, gateOrder),
            new ResultRow(Constants.ResultMessages.LabelStatus, status),
        };

        /// <summary>
        /// 레벨4(발전소) 결과 행 — 고정 상황 / 체험자가 만약에 연결한 조건식 / 놀이시설 끄기 조건(만약)·병원 전력 유지.
        /// 뒤 두 항목은 놀이시설·병원 채점과 같은 기준이라 효율 %가 왜 그렇게 나왔는지 화면에서 읽힌다.
        /// </summary>
        private static List<ResultRow> BuildPowerPlantRows(string condition, string amusementPowerCut, string hospitalPowerKept, string status) => new()
        {
            new ResultRow(Constants.ResultMessages.LabelSituation, Constants.ResultMessages.PowerPlantSituation),
            new ResultRow(Constants.ResultMessages.LabelCondition, OrNoValue(condition)),
            new ResultRow(Constants.ResultMessages.LabelAmusement, amusementPowerCut),
            new ResultRow(Constants.ResultMessages.LabelHospital, hospitalPowerKept),
            new ResultRow(Constants.ResultMessages.LabelStatus, status),
        };

        /// <summary>
        /// 레벨5(미래에너지) 결과 행 — 함수 사용 여부 / 에너지 블록별로 함수 안에 넣었는지 / 전력 수급 상태.
        /// 에너지 행은 채점(함수 안 에너지 수)과 같은 기준이라 효율 %가 왜 그렇게 나왔는지 화면에서 읽힌다.
        /// </summary>
        private static List<ResultRow> BuildFutureEnergyRows(bool functionUsed, IReadOnlyCollection<string> energiesInFunction, string status)
        {
            List<ResultRow> rows = new() { new ResultRow(Constants.ResultMessages.LabelFunctionUsed, OnOff(functionUsed)) };
            foreach (string energy in Constants.BlockLabels.FutureEnergies)
                rows.Add(new ResultRow(energy, OnOff(Contains(energiesInFunction, energy))));
            rows.Add(new ResultRow(Constants.ResultMessages.LabelStatus, status));
            return rows;
        }

        /// <summary>
        /// 목록에 값이 들어 있는지 확인한다 (목록이 없으면 false).
        /// </summary>
        private static bool Contains(IReadOnlyCollection<string> values, string value)
        {
            if (values is null) return false;
            foreach (string v in values)
                if (v == value) return true;
            return false;
        }
    }
}
