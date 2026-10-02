using System.Collections.Generic;
using Data;
using Game;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 블록 연결을 계층 이름 탐색 대신 소켓 소유 관계로 따라가도록 바꾼 뒤에도
    /// 컴파일 결과(명령 목록·실패 사유·문제 블록)가 그대로인지 검증한다.
    /// </summary>
    public class BlockCompilerTests
    {
        private CodingZone _zone;
        private Transform _inventory;
        private CodingBlock _start;
        private CodingBlock _end;

        /// <summary>
        /// 코딩 패널과 인벤토리, 시작하기/완성하기 블록을 준비한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            CodingBlock.RestrictMainChainToFunction = false;
            _zone = BlockTestUtil.MakeZone();
            _inventory = new GameObject("Inventory", typeof(RectTransform)).transform;
            _start = BlockTestUtil.MakeBlock(_zone, "시작하기", BlockCategory.Control, _zone.transform, role: ControlRole.Start);
            _end = BlockTestUtil.MakeBlock(_zone, "완성하기", BlockCategory.Control, _zone.transform, role: ControlRole.End);
        }

        /// <summary>
        /// 테스트가 만든 오브젝트를 모두 파괴한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_zone) Object.DestroyImmediate(_zone.gameObject);
            if (_inventory) Object.DestroyImmediate(_inventory.gameObject);
        }

        /// <summary>
        /// 시작하기 → 값이 연결된 명령 → 완성하기 체인이면 성공하고 명령과 값이 그대로 읽힌다.
        /// </summary>
        [Test]
        public void 값이_연결된_명령_체인은_컴파일에_성공한다()
        {
            CodingBlock cmd = BlockTestUtil.MakeBlock(_zone, "풍차의 날개 방향", BlockCategory.Command, _inventory, ValueKind.Direction);
            CodingBlock value = BlockTestUtil.MakeBlock(_zone, Constants.Directions.East, BlockCategory.Value, _inventory, ValueKind.Direction);

            ChainOutSocket.OfBlock(_start).Accept(cmd);
            cmd.GetSocket<ValueOutSocket>().Accept(value);
            ChainOutSocket.OfBlock(cmd).Accept(_end);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Instructions.Count);
            CommandInstruction instr = result.Instructions[0] as CommandInstruction;
            Assert.IsNotNull(instr, "첫 명령이 CommandInstruction이 아님");
            Assert.AreEqual("풍차의 날개 방향", instr.Command);
            Assert.AreEqual(Constants.Directions.East, instr.Value);
            Assert.AreSame(value, instr.ValueSource);
        }

        /// <summary>
        /// 값 슬롯이 비어 있는 명령이 있으면 그 명령 블록을 문제 블록으로 지목하며 실패한다.
        /// </summary>
        [Test]
        public void 값이_빠진_명령은_그_블록을_지목하며_실패한다()
        {
            CodingBlock cmd = BlockTestUtil.MakeBlock(_zone, "풍차의 날개 방향", BlockCategory.Command, _inventory, ValueKind.Direction);

            ChainOutSocket.OfBlock(_start).Accept(cmd);
            ChainOutSocket.OfBlock(cmd).Accept(_end);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.ErrorBlocks);
            Assert.AreSame(cmd, result.ErrorBlocks[0]);
        }

        /// <summary>
        /// 인벤토리에 남은 실행 블록이 있으면 '사용되지 않은 블록' 오류로 분류된다
        /// (비활성 탭의 블록도 코딩 패널의 블록 목록으로 검사 대상에 포함돼야 한다).
        /// </summary>
        [Test]
        public void 인벤토리에_남은_실행_블록은_미사용_오류로_분류된다()
        {
            CodingBlock used = BlockTestUtil.MakeBlock(_zone, "개방하기", BlockCategory.Command, _inventory);
            CodingBlock unused = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.While, BlockCategory.FlowControl, _inventory);
            unused.gameObject.SetActive(false); // 선택되지 않은 탭

            ChainOutSocket.OfBlock(_start).Accept(used);
            ChainOutSocket.OfBlock(used).Accept(_end);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CompileErrorKind.UnusedBlocks, result.ErrorKind);
            CollectionAssert.Contains(result.ErrorBlocks, unused);
        }

        /// <summary>
        /// 쓰지 않은 움직이기(Command) 블록은 인벤토리에 남거나 코딩 영역에 떠 있어도 컴파일에 성공한다.
        /// </summary>
        [Test]
        public void 쓰지_않은_움직이기_블록은_오류가_아니다()
        {
            CodingBlock used = BlockTestUtil.MakeBlock(_zone, "개방하기", BlockCategory.Command, _inventory);
            CodingBlock unused = BlockTestUtil.MakeBlock(_zone, "폐쇄하기", BlockCategory.Command, _inventory);
            unused.gameObject.SetActive(false); // 선택되지 않은 탭
            BlockTestUtil.MakeBlock(_zone, "수문 열기", BlockCategory.Command, _zone.transform); // 코딩 영역에 방치

            ChainOutSocket.OfBlock(_start).Accept(used);
            ChainOutSocket.OfBlock(used).Accept(_end);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Instructions.Count);
        }

        /// <summary>
        /// 시작하기와 완성하기를 바로 이어 사이에 블록이 없으면 두 블록을 지목하며 실패한다.
        /// </summary>
        [Test]
        public void 시작하기와_완성하기만_이으면_두_블록을_지목하며_실패한다()
        {
            BlockTestUtil.MakeBlock(_zone, "개방하기", BlockCategory.Command, _inventory);

            ChainOutSocket.OfBlock(_start).Accept(_end);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsFalse(result.Success);
            CollectionAssert.AreEquivalent(new[] { _start, _end }, result.ErrorBlocks);
        }

        /// <summary>
        /// 반복하기 내부 체인은 Inner 소켓 소유 관계를 따라 본문으로 읽힌다.
        /// </summary>
        [Test]
        public void 반복하기_내부_블록이_본문으로_읽힌다()
        {
            CodingBlock repeat = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.While, BlockCategory.FlowControl, _inventory);
            InnerSocket inner = BlockTestUtil.AddInnerSocket(repeat);
            CodingBlock cmd = BlockTestUtil.MakeBlock(_zone, "개방하기", BlockCategory.Command, _inventory);

            ChainOutSocket.OfBlock(_start).Accept(repeat);
            inner.Accept(cmd);
            ChainOutSocket.OfBlock(repeat).Accept(_end);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsTrue(result.Success, result.Error);
            RepeatInstruction rep = result.Instructions[0] as RepeatInstruction;
            Assert.IsNotNull(rep, "첫 명령이 반복하기가 아님");
            List<BlockInstruction> body = rep.Body;
            Assert.AreEqual(1, body.Count);
            Assert.AreSame(cmd, body[0].Source);
        }

        /// <summary>
        /// 만약 안에 아니면을 놓고 그 아래를 비워 두면 아니면 블록을 지목하며 실패한다.
        /// </summary>
        [Test]
        public void 아니면_아래가_비어_있으면_아니면_블록을_지목하며_실패한다()
        {
            CodingBlock elseBlock = BuildIfElse(out _, out _);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsFalse(result.Success, "빈 아니면이 컴파일에 성공함");
            CollectionAssert.AreEquivalent(new[] { elseBlock }, result.ErrorBlocks);
        }

        /// <summary>
        /// 아니면 아래에 블록이 있으면 성공하고, 아니면 앞뒤 블록이 각각 Then·Else로 읽힌다.
        /// </summary>
        [Test]
        public void 아니면_아래에_블록이_있으면_Else로_읽힌다()
        {
            CodingBlock elseBlock = BuildIfElse(out CodingBlock open, out CodingBlock close);
            ChainOutSocket.OfBlock(elseBlock).Accept(close);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsTrue(result.Success, result.Error);
            IfInstruction ifInstr = result.Instructions[0] as IfInstruction;
            Assert.IsNotNull(ifInstr, "첫 명령이 만약이 아님");
            Assert.IsTrue(ifInstr.HasElseMarker);
            Assert.AreEqual(1, ifInstr.Then.Count);
            Assert.AreSame(open, ifInstr.Then[0].Source);
            Assert.AreEqual(1, ifInstr.Else.Count);
            Assert.AreSame(close, ifInstr.Else[0].Source);
        }

        /// <summary>
        /// 시작하기 → 만약(5m 이상){ 수문 열기 → 아니면 } → 완성하기 를 조립한다. 수문 닫기는 아직 잇지 않는다.
        /// </summary>
        private CodingBlock BuildIfElse(out CodingBlock open, out CodingBlock close)
        {
            CodingBlock ifBlock = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.If, BlockCategory.FlowControl, _inventory);
            ValueOutSocket conditionSlot = BlockTestUtil.AddConditionSocket(ifBlock);
            InnerSocket inner = BlockTestUtil.AddInnerSocket(ifBlock);
            CodingBlock condition = BlockTestUtil.MakeBlock(_zone, "5m 이상", BlockCategory.Condition, _inventory);
            CodingBlock elseBlock = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.Else, BlockCategory.Else, _inventory);
            open = BlockTestUtil.MakeBlock(_zone, "수문 열기", BlockCategory.Command, _inventory);
            close = BlockTestUtil.MakeBlock(_zone, "수문 닫기", BlockCategory.Command, _inventory);

            ChainOutSocket.OfBlock(_start).Accept(ifBlock);
            conditionSlot.Accept(condition);
            inner.Accept(open);
            ChainOutSocket.OfBlock(open).Accept(elseBlock);
            ChainOutSocket.OfBlock(ifBlock).Accept(_end);
            return elseBlock;
        }
    }
}
