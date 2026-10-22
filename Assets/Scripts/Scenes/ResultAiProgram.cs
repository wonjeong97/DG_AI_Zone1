using System;
using Data;
using Game;
using Game.Runtime;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Scenes
{
    // 결과 화면 'AI 코딩 결과'에 조립할 레벨별 정답 블록 모양을 만든다 — 채점 규칙상 만점인 조합이다.
    public static class ResultAiProgram
    {
        /// <summary>
        /// 레벨의 정답 블록 모양을 문제 값(높이·시간)과 정답 방향에 맞춰 레벨 블록 목록의 블록으로 만든다(목록에 없는 블록은 경고하고 그 안쪽·값 블록과 함께 빼고, 시작하기~완성하기는 남은 블록으로 잇는다).
        /// </summary>
        public static ProgramShape Build(LevelData level, string questionValue, string correctAnswer, ILogger logger = null)
        {
            ProgramShape shape = new ProgramShape();
            BlockEntry[] blocks = level && level.blockLayout ? level.blockLayout.inventoryBlocks : null;
            if (blocks is null)
            {
                if (logger != null) logger.ZLogWarning($"[ResultAiProgram] 레벨 블록 목록이 없어 AI 정답 블록을 만들지 못했습니다.");
                return shape;
            }

            Finder find = new Finder(blocks, logger);
            ProgramBlock end = find.Control(ControlRole.End);
            ProgramBlock body;

            switch (level.kind)
            {
                case LevelKind.Wind:
                    // 시작하기 → 반복하기(무한) { 풍차의 날개 방향(바람 방향) } → 완성하기
                    body = Inside(find.One(e => e.category == BlockCategory.FlowControl && IsRepeat(e.label), "반복하기"),
                        WithValue(find.One(e => e.category == BlockCategory.Command && e.valueKind == ValueKind.Direction, "방향 명령"),
                            find.Label(BlockCategory.Value, correctAnswer)));
                    break;

                case LevelKind.Hydro:
                    // 시작하기 → 만약 [문제 높이 이상] { 수문 열기 → 아니면 → 수문 닫기 } → 완성하기
                    body = Inside(
                        WithValue(find.Label(BlockCategory.FlowControl, Constants.BlockLabels.If),
                            find.One(e => e.category == BlockCategory.Condition && IsHeightCondition(e.label, questionValue), "높이 조건")),
                        Chain(find.Label(BlockCategory.Command, Constants.BlockLabels.HydroOpen),
                            find.Label(BlockCategory.Else, Constants.BlockLabels.Else),
                            find.Label(BlockCategory.Command, Constants.BlockLabels.HydroClose)));
                    break;

                case LevelKind.PowerPlant:
                    // 시작하기 → 만약 [전기 과부하 그리고 밤] { 놀이시설 불 끄기 } → 반복하기(무한) { 병원 불 켜기 } → 완성하기
                    ProgramBlock overload = find.One(e => e.category == BlockCategory.Condition && e.valueKind == ValueKind.None
                        && e.label != Constants.BlockLabels.SpareCondition, "과부하 조건");
                    ProgramBlock and = find.Label(BlockCategory.Logic, Constants.BlockLabels.And);
                    ProgramBlock time = find.One(e => e.category == BlockCategory.Condition && e.valueKind == ValueKind.Time
                        && e.label != Constants.BlockLabels.DayCondition, "시간 조건");
                    if (overload is not null) overload.Condition = and;
                    if (and is not null) and.Condition = time;
                    else if (overload is not null) overload.Condition = time;

                    body = Chain(
                        Inside(WithValue(find.Label(BlockCategory.FlowControl, Constants.BlockLabels.If), overload),
                            find.Label(BlockCategory.Command, Constants.BlockLabels.AmusementOff)),
                        Inside(find.One(e => e.category == BlockCategory.FlowControl && IsRepeat(e.label), "반복하기"),
                            find.Label(BlockCategory.Command, Constants.BlockLabels.HospitalOn)));
                    break;

                case LevelKind.FutureEnergy:
                    // 시작하기 → 미래 에너지 만들기 → 완성하기, 함수 정의 { 태양광 → 풍력 → 수력 발전 → 스마트 도시 발전소 }
                    body = find.One(e => e.category == BlockCategory.Function, "함수 블록");

                    ProgramBlock[] energies = new ProgramBlock[Constants.BlockLabels.FutureEnergies.Count];
                    for (int i = 0; i < energies.Length; i++)
                        energies[i] = find.Label(BlockCategory.Command, Constants.BlockLabels.FutureEnergies[i]);

                    ProgramBlock definition = Inside(find.One(e => e.category == BlockCategory.FunctionDef, "함수 정의 블록"), Chain(energies));
                    if (definition is not null) shape.FunctionDefs.Add(definition);
                    break;

                default:
                    // 시작하기 → 태양광 패널의 개수(최고 점수 개수) → 태양광 패널의 방향(정답 방향) → 완성하기
                    body = Chain(
                        WithValue(find.One(e => e.category == BlockCategory.Command && e.valueKind == ValueKind.Count, "개수 명령"),
                            find.Label(BlockCategory.Value, BlockScorer.GetBestCount())),
                        WithValue(find.One(e => e.category == BlockCategory.Command && e.valueKind == ValueKind.Direction, "방향 명령"),
                            find.Label(BlockCategory.Value, correctAnswer)));
                    break;
            }

            shape.Start = Chain(find.Control(ControlRole.Start), body, end);
            return shape;
        }

        /// <summary>
        /// 반복하기 블록 라벨인지 확인한다 — 레이아웃 라벨은 '반복하기(무한)'처럼 표기가 붙는다.
        /// </summary>
        private static bool IsRepeat(string label) => label != null && label.Contains(Constants.BlockLabels.RepeatKeyword);

        /// <summary>
        /// 높이 조건 라벨('5m 이상')이 문제 높이('5m')와 같은 높이인지 확인한다 — '1m'가 '10m 이상'에 맞지 않도록 공백까지 본다.
        /// </summary>
        private static bool IsHeightCondition(string label, string height)
            => !string.IsNullOrEmpty(height) && label != null && label.StartsWith(height + " ", StringComparison.Ordinal);

        /// <summary>
        /// 블록들을 위에서 아래로 잇고 맨 위 블록을 돌려준다 — null인 블록은 건너뛰고, 이미 다음 블록이 이어진 묶음은 그 끝에 잇는다.
        /// </summary>
        private static ProgramBlock Chain(params ProgramBlock[] blocks)
        {
            ProgramBlock head = null;
            ProgramBlock tail = null;
            foreach (ProgramBlock block in blocks)
            {
                if (block is null) continue;

                if (tail is null) head = block;
                else tail.Next = block;

                tail = block;
                while (tail.Next is not null) tail = tail.Next;
            }
            return head;
        }

        /// <summary>
        /// 명령·만약 블록의 값 슬롯에 블록을 끼운다.
        /// </summary>
        private static ProgramBlock WithValue(ProgramBlock block, ProgramBlock value)
        {
            if (block is not null) block.Value = value;
            return block;
        }

        /// <summary>
        /// 반복하기·만약·함수 정의 블록의 첫 안쪽 소켓에 블록 체인을 넣는다.
        /// </summary>
        private static ProgramBlock Inside(ProgramBlock container, ProgramBlock first)
        {
            if (container is not null) container.Inners.Add(first);
            return container;
        }

        // 레벨 블록 목록에서 정답에 쓸 블록을 찾아 새 ProgramBlock으로 만든다 — 못 찾으면 경고하고 null.
        private readonly struct Finder
        {
            private readonly BlockEntry[] _blocks;
            private readonly ILogger _logger;

            public Finder(BlockEntry[] blocks, ILogger logger)
            {
                _blocks = blocks;
                _logger = logger;
            }

            /// <summary>
            /// 시작하기·완성하기 블록을 찾는다.
            /// </summary>
            public ProgramBlock Control(ControlRole role)
                => One(e => e.category == BlockCategory.Control && e.controlRole == role, role.ToString());

            /// <summary>
            /// 카테고리와 라벨이 같은 블록을 찾는다.
            /// </summary>
            public ProgramBlock Label(BlockCategory category, string label)
                => One(e => e.category == category && e.label == label, label);

            /// <summary>
            /// 조건에 맞는 첫 블록을 찾는다 — 없으면 what으로 경고한다.
            /// </summary>
            public ProgramBlock One(Func<BlockEntry, bool> match, string what)
            {
                foreach (BlockEntry entry in _blocks)
                    if (entry is not null && match(entry)) return ProgramBlock.From(entry);

                if (_logger != null) _logger.ZLogWarning($"[ResultAiProgram] 레벨 블록 목록에서 '{what}' 블록을 찾지 못해 AI 정답에서 뺍니다.");
                return null;
            }
        }
    }
}
