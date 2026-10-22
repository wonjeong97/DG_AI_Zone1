using System.Collections.Generic;
using Data;
using Game;
using Game.Runtime;
using NUnit.Framework;
using Scenes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 결과 화면 블록 조립에 쓰는 블록 모양 — 코딩 패널에서 읽어 오는 모양(ProgramCapture)과 레벨별 AI 정답 모양(ResultAiProgram)을 검증한다.
    /// </summary>
    public class ResultProgramTests
    {
        private CodingZone _zone;

        /// <summary>
        /// 코딩 패널을 준비한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _zone = BlockTestUtil.MakeZone();
        }

        /// <summary>
        /// 테스트가 만든 오브젝트를 모두 파괴한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_zone) Object.DestroyImmediate(_zone.gameObject);
        }

        /// <summary>
        /// 시작하기에서 이어진 블록을 값 슬롯·안쪽 소켓까지 소켓 연결 그대로 읽고, 연결되지 않은 블록은 뺀다.
        /// </summary>
        [Test]
        public void 시작하기에서_이어진_블록을_소켓_연결_그대로_읽는다()
        {
            CodingBlock start = BlockTestUtil.MakeBlock(_zone, "시작하기", BlockCategory.Control, _zone.transform, role: ControlRole.Start);
            CodingBlock command = BlockTestUtil.MakeBlock(_zone, "방향", BlockCategory.Command, _zone.transform, ValueKind.Direction);
            CodingBlock value = BlockTestUtil.MakeBlock(_zone, "동쪽", BlockCategory.Value, _zone.transform, ValueKind.Direction);
            CodingBlock repeat = BlockTestUtil.MakeBlock(_zone, "반복하기(무한)", BlockCategory.FlowControl, _zone.transform);
            InnerSocket repeatInner = BlockTestUtil.AddInnerSocket(repeat);
            CodingBlock inside = BlockTestUtil.MakeBlock(_zone, "안쪽", BlockCategory.Command, _zone.transform);
            CodingBlock end = BlockTestUtil.MakeBlock(_zone, "완성하기", BlockCategory.Control, _zone.transform, role: ControlRole.End);
            BlockTestUtil.MakeBlock(_zone, "떨어진 블록", BlockCategory.Command, _zone.transform);

            ChainOutSocket.OfBlock(start).Accept(command);
            command.GetSocket<ValueOutSocket>().Accept(value);
            ChainOutSocket.OfBlock(command).Accept(repeat);
            repeatInner.Accept(inside);
            ChainOutSocket.OfBlock(repeat).Accept(end);

            ProgramShape shape = ProgramCapture.Capture(_zone);

            CollectionAssert.AreEqual(new[] { "시작하기", "방향", "반복하기(무한)", "완성하기" }, ChainLabels(shape.Start));
            ProgramBlock capturedCommand = shape.Start.Next;
            Assert.AreEqual(ValueKind.Direction, capturedCommand.ValueKind);
            Assert.AreEqual("동쪽", capturedCommand.Value?.Label, "값 슬롯에 붙은 블록을 읽지 못함");

            ProgramBlock capturedRepeat = capturedCommand.Next;
            Assert.AreEqual(1, capturedRepeat.Inners.Count, "안쪽 소켓 수가 블록과 다름");
            CollectionAssert.AreEqual(new[] { "안쪽" }, ChainLabels(capturedRepeat.Inners[0]));
            Assert.AreEqual(ControlRole.End, capturedRepeat.Next.ControlRole);
            Assert.IsEmpty(shape.FunctionDefs);
        }

        /// <summary>
        /// 체인과 따로 놓인 함수 정의 블록도 안쪽 블록과 함께 읽는다.
        /// </summary>
        [Test]
        public void 함수_정의_블록을_따로_읽는다()
        {
            BlockTestUtil.MakeBlock(_zone, "시작하기", BlockCategory.Control, _zone.transform, role: ControlRole.Start);
            CodingBlock definition = BlockTestUtil.MakeBlock(_zone, "미래 에너지 만들기", BlockCategory.FunctionDef, _zone.transform);
            InnerSocket inner = BlockTestUtil.AddInnerSocket(definition);
            CodingBlock energy = BlockTestUtil.MakeBlock(_zone, "태양광", BlockCategory.Command, _zone.transform);
            inner.Accept(energy);

            ProgramShape shape = ProgramCapture.Capture(_zone);

            Assert.AreEqual(1, shape.FunctionDefs.Count);
            CollectionAssert.AreEqual(new[] { "태양광" }, ChainLabels(shape.FunctionDefs[0].Inners[0]));
        }

#if UNITY_EDITOR
        /// <summary>
        /// 레벨1 AI 정답은 최고 점수 개수와 문제의 정답 방향을 끼운 두 명령이다.
        /// </summary>
        [Test]
        public void 레벨1_AI_정답은_최고_개수와_정답_방향이다()
        {
            LevelData level = LoadLevel(LevelKind.Solar);
            foreach (QuestionOption option in level.questionOptions)
            {
                ProgramShape shape = ResultAiProgram.Build(level, option.value, option.correctAnswer);

                Assert.AreEqual(4, ChainLabels(shape.Start).Count, $"{option.value} 체인 길이가 다름");
                ProgramBlock count = shape.Start.Next;
                ProgramBlock direction = count.Next;
                Assert.AreEqual(ValueKind.Count, count.ValueKind);
                Assert.AreEqual(BlockScorer.GetBestCount(), count.Value?.Label);
                Assert.AreEqual(ValueKind.Direction, direction.ValueKind);
                Assert.AreEqual(option.correctAnswer, direction.Value?.Label, $"{option.value}의 정답 방향과 다름");
                Assert.AreEqual(ControlRole.End, direction.Next?.ControlRole);
            }
        }

        /// <summary>
        /// 레벨2 AI 정답은 반복하기 안에 바람 방향을 끼운 날개 방향 명령이다.
        /// </summary>
        [Test]
        public void 레벨2_AI_정답은_반복하기_안의_바람_방향이다()
        {
            LevelData level = LoadLevel(LevelKind.Wind);
            foreach (QuestionOption option in level.questionOptions)
            {
                ProgramShape shape = ResultAiProgram.Build(level, option.value, option.correctAnswer);

                ProgramBlock repeat = shape.Start.Next;
                StringAssert.Contains(Constants.BlockLabels.RepeatKeyword, repeat.Label);
                ProgramBlock command = repeat.Inners[0];
                Assert.AreEqual(option.correctAnswer, command.Value?.Label, $"{option.value} 바람의 날개 방향이 다름");
                Assert.AreEqual(ControlRole.End, repeat.Next?.ControlRole);
            }
        }

        /// <summary>
        /// 레벨3 AI 정답은 문제 높이 조건의 만약 안에 수문 열기 → 아니면 → 수문 닫기다('1m'가 '10m 이상'에 붙지 않는다).
        /// </summary>
        [Test]
        public void 레벨3_AI_정답은_문제_높이_조건과_수문_순서다()
        {
            LevelData level = LoadLevel(LevelKind.Hydro);
            foreach (QuestionOption option in level.questionOptions)
            {
                ProgramShape shape = ResultAiProgram.Build(level, option.value, option.correctAnswer);

                ProgramBlock ifBlock = shape.Start.Next;
                Assert.AreEqual(Constants.BlockLabels.If, ifBlock.Label);
                Assert.AreEqual(option.value + " 이상", ifBlock.Value?.Label, $"{option.value} 높이 조건이 다름");
                CollectionAssert.AreEqual(
                    new[] { Constants.BlockLabels.HydroOpen, Constants.BlockLabels.Else, Constants.BlockLabels.HydroClose },
                    ChainLabels(ifBlock.Inners[0]));
                Assert.AreEqual(ControlRole.End, ifBlock.Next?.ControlRole);
            }
        }

        /// <summary>
        /// 레벨4 AI 정답은 '전기 과부하 그리고 밤' 만약 안의 놀이시설 끄기와, 반복하기 안의 병원 켜기다(함정 블록 없음).
        /// </summary>
        [Test]
        public void 레벨4_AI_정답은_그리고_조건과_반복_병원_켜기다()
        {
            LevelData level = LoadLevel(LevelKind.PowerPlant);
            QuestionOption option = level.questionOptions[0];
            ProgramShape shape = ResultAiProgram.Build(level, option.value, option.correctAnswer);

            ProgramBlock ifBlock = shape.Start.Next;
            Assert.AreEqual(Constants.BlockLabels.If, ifBlock.Label);
            ProgramBlock first = ifBlock.Value;
            Assert.AreEqual(Constants.BlockLabels.And, first?.Condition?.Label);
            Assert.AreEqual(option.value, first.Condition.Condition?.Label, "그리고 뒤 조건이 문제 시간과 다름");
            Assert.AreNotEqual(Constants.BlockLabels.SpareCondition, first.Label);
            CollectionAssert.AreEqual(new[] { Constants.BlockLabels.AmusementOff }, ChainLabels(ifBlock.Inners[0]));

            ProgramBlock repeat = ifBlock.Next;
            StringAssert.Contains(Constants.BlockLabels.RepeatKeyword, repeat.Label);
            CollectionAssert.AreEqual(new[] { Constants.BlockLabels.HospitalOn }, ChainLabels(repeat.Inners[0]));
            Assert.AreEqual(ControlRole.End, repeat.Next?.ControlRole);
        }

        /// <summary>
        /// 레벨5 AI 정답은 함수 호출 하나와, 에너지 블록 넷을 모두 넣은 함수 정의다.
        /// </summary>
        [Test]
        public void 레벨5_AI_정답은_함수_호출과_에너지를_모두_넣은_정의다()
        {
            LevelData level = LoadLevel(LevelKind.FutureEnergy);
            QuestionOption option = level.questionOptions[0];
            ProgramShape shape = ResultAiProgram.Build(level, option.value, option.correctAnswer);

            Assert.AreEqual(BlockCategory.Function, shape.Start.Next?.Category);
            Assert.AreEqual(ControlRole.End, shape.Start.Next.Next?.ControlRole);
            Assert.AreEqual(1, shape.FunctionDefs.Count);
            CollectionAssert.AreEqual(Constants.BlockLabels.FutureEnergies, ChainLabels(shape.FunctionDefs[0].Inners[0]));
        }

        /// <summary>
        /// 레벨 종류에 맞는 LevelData 에셋을 불러온다.
        /// </summary>
        private static LevelData LoadLevel(LevelKind kind)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/Data" }))
            {
                LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
                if (level && level.kind == kind) return level;
            }

            Assert.Fail($"{kind} LevelData 에셋을 찾지 못함");
            return null;
        }
#endif

        /// <summary>
        /// 맨 위 블록부터 아래로 이어진 블록 라벨을 모은다.
        /// </summary>
        private static List<string> ChainLabels(ProgramBlock head)
        {
            List<string> labels = new List<string>();
            for (ProgramBlock block = head; block is not null; block = block.Next)
                labels.Add(block.Label);
            return labels;
        }
    }
}
