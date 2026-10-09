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
        /// 함수 블록이 있는 레벨이어도 움직이기 블록을 시작하기 아래에 바로 이을 수 있고, 함수 호출 없이도 컴파일된다.
        /// </summary>
        [Test]
        public void 함수_없이_시작하기_아래에_움직이기_블록을_이어도_성공한다()
        {
            CodingBlock solar = BlockTestUtil.MakeBlock(_zone, "태양광", BlockCategory.Command, _inventory);
            BlockTestUtil.MakeBlock(_zone, "미래 에너지 만들기", BlockCategory.FunctionDef, _inventory);
            BlockTestUtil.MakeBlock(_zone, "미래 에너지 만들기", BlockCategory.Function, _inventory);

            ChainOutSocket.OfBlock(_start).Accept(solar);
            ChainOutSocket.OfBlock(solar).Accept(_end);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreSame(solar, result.Instructions[0].Source);
        }

        /// <summary>
        /// 함수 호출을 이었는데 함수 정의 블록이 인벤토리에 남아 있으면 두 블록을 지목하며 실패하고, 인벤토리 탭 전환 대상이 된다.
        /// </summary>
        [Test]
        public void 함수_호출을_이었는데_함수_정의가_인벤토리에_있으면_실패한다()
        {
            CodingBlock def = BlockTestUtil.MakeBlock(_zone, "미래 에너지 만들기", BlockCategory.FunctionDef, _inventory);
            CodingBlock call = BuildFunctionCallChain();

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsFalse(result.Success, "함수 정의 없이 호출이 컴파일에 성공함");
            Assert.AreEqual(CompileErrorKind.UnusedBlocks, result.ErrorKind);
            CollectionAssert.AreEqual(new[] { def, call }, result.ErrorBlocks);
        }

        /// <summary>
        /// 함수 정의 블록이 코딩 영역에 있으면 안이 비어 있어도 컴파일에 성공한다.
        /// </summary>
        [Test]
        public void 함수_정의가_비어_있어도_코딩_영역에_있으면_성공한다()
        {
            BlockTestUtil.MakeBlock(_zone, "미래 에너지 만들기", BlockCategory.FunctionDef, _zone.transform);
            CodingBlock call = BuildFunctionCallChain();

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsTrue(result.Success, result.Error);
            FunctionInstruction fn = result.Instructions[0] as FunctionInstruction;
            Assert.IsNotNull(fn, "첫 명령이 함수 호출이 아님");
            Assert.AreSame(call, fn.Source);
            Assert.AreEqual(0, fn.Body.Count);
        }

        /// <summary>
        /// 시작하기 → 함수 호출 → 완성하기 를 잇고 함수 호출 블록을 반환한다.
        /// </summary>
        private CodingBlock BuildFunctionCallChain()
        {
            CodingBlock call = BlockTestUtil.MakeBlock(_zone, "미래 에너지 만들기", BlockCategory.Function, _inventory);
            ChainOutSocket.OfBlock(_start).Accept(call);
            ChainOutSocket.OfBlock(call).Accept(_end);
            return call;
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

        /// <summary>
        /// 만약 조건을 '조건 그리고 조건'으로 이으면 성공하고 왼쪽·오른쪽 조건으로 읽힌다.
        /// </summary>
        [Test]
        public void 조건_두_개를_이으면_성공하고_좌우_조건으로_읽힌다()
        {
            ValueOutSocket conditionSlot = BuildIfWithBody();
            CodingBlock overload = MakeCondition("전기 과부하");
            CodingBlock and = MakeLogic(Constants.BlockLabels.And);
            CodingBlock night = MakeCondition("밤");

            conditionSlot.Accept(overload);
            overload.GetSocket<ConditionOutSocket>().Accept(and);
            and.GetSocket<ConditionOutSocket>().Accept(night);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsTrue(result.Success, result.Error);
            LogicConditionExpr logic = (result.Instructions[0] as IfInstruction)?.Condition as LogicConditionExpr;
            Assert.IsNotNull(logic, "만약 조건이 그리고/또는 조건식으로 읽히지 않음");
            Assert.AreSame(overload, logic.Left.Source);
            Assert.AreSame(night, logic.Right.Source);
            Assert.IsNull(logic.Overflow);
        }

        /// <summary>
        /// 조건을 세 개 이상 이으면 두 번째 조건 뒤에 이은 블록들을 지목하며 실패한다 —
        /// 그대로 두면 세 번째 조건(함정 '낮' 등)이 채점에서 빠져 조건 점수가 만점이 된다.
        /// </summary>
        [Test]
        public void 조건을_세_개_이상_이으면_뒤에_이은_블록을_지목하며_실패한다()
        {
            ValueOutSocket conditionSlot = BuildIfWithBody();
            CodingBlock overload = MakeCondition("전기 과부하");
            CodingBlock and = MakeLogic(Constants.BlockLabels.And);
            CodingBlock night = MakeCondition("밤");
            CodingBlock or = MakeLogic("또는");
            CodingBlock day = MakeCondition(Constants.BlockLabels.DayCondition);

            conditionSlot.Accept(overload);
            overload.GetSocket<ConditionOutSocket>().Accept(and);
            and.GetSocket<ConditionOutSocket>().Accept(night);
            night.GetSocket<ConditionOutSocket>().Accept(or);
            or.GetSocket<ConditionOutSocket>().Accept(day);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsFalse(result.Success, "조건 세 개가 컴파일에 성공함");
            Assert.AreEqual(Constants.CompilerMessages.ConditionChainTooLong, result.Error);
            CollectionAssert.AreEqual(new[] { or, day }, result.ErrorBlocks);
        }

        /// <summary>
        /// 연결하지 않은 만약 안에 아니면이 있으면, 아니면 위치가 아니라 연결하지 않은 만약을 미사용 블록으로 지목한다.
        /// </summary>
        [Test]
        public void 연결하지_않은_만약_안의_아니면은_만약을_미사용으로_지목한다()
        {
            CodingBlock ifBlock = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.If, BlockCategory.FlowControl, _zone.transform);
            BlockTestUtil.AddConditionSocket(ifBlock).Accept(MakeCondition("5m 이상"));
            InnerSocket inner = BlockTestUtil.AddInnerSocket(ifBlock);
            CodingBlock open = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.HydroOpen, BlockCategory.Command, _inventory);
            CodingBlock elseBlock = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.Else, BlockCategory.Else, _inventory);
            CodingBlock close = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.HydroClose, BlockCategory.Command, _inventory);
            inner.Accept(open);
            ChainOutSocket.OfBlock(open).Accept(elseBlock);
            ChainOutSocket.OfBlock(elseBlock).Accept(close);

            CodingBlock cmd = BlockTestUtil.MakeBlock(_zone, "개방하기", BlockCategory.Command, _inventory);
            ChainOutSocket.OfBlock(_start).Accept(cmd);
            ChainOutSocket.OfBlock(cmd).Accept(_end);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CompileErrorKind.UnusedBlocks, result.ErrorKind, result.Error);
            CollectionAssert.Contains(result.ErrorBlocks, ifBlock);
            CollectionAssert.DoesNotContain(result.ErrorBlocks, elseBlock);
        }

        /// <summary>
        /// 함수 정의 안에 함수 호출을 넣으면 끝없이 펼치지 않고 그 호출 블록을 지목하며 실패한다.
        /// </summary>
        [Test]
        public void 함수_정의_안의_함수_호출은_지목하며_실패한다()
        {
            CodingBlock def = BlockTestUtil.MakeBlock(_zone, "미래 에너지 만들기", BlockCategory.FunctionDef, _zone.transform);
            InnerSocket defInner = BlockTestUtil.AddInnerSocket(def);
            CodingBlock innerCall = BlockTestUtil.MakeBlock(_zone, "미래 에너지 만들기", BlockCategory.Function, _inventory);
            defInner.Accept(innerCall);
            BuildFunctionCallChain();

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsFalse(result.Success, "함수 정의 안의 함수 호출이 컴파일에 성공함");
            Assert.AreEqual(Constants.CompilerMessages.FunctionCallInsideDef, result.Error);
            CollectionAssert.AreEqual(new[] { innerCall }, result.ErrorBlocks);
        }

        /// <summary>
        /// 만약 하나에 아니면을 두 개 넣으면 두 번째 아니면을 지목하며 실패한다.
        /// </summary>
        [Test]
        public void 만약_하나에_아니면을_두_개_넣으면_두_번째를_지목하며_실패한다()
        {
            CodingBlock firstElse = BuildIfElse(out _, out CodingBlock close);
            CodingBlock secondElse = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.Else, BlockCategory.Else, _inventory);
            CodingBlock after = BlockTestUtil.MakeBlock(_zone, "개방하기", BlockCategory.Command, _inventory);
            ChainOutSocket.OfBlock(firstElse).Accept(close);
            ChainOutSocket.OfBlock(close).Accept(secondElse);
            ChainOutSocket.OfBlock(secondElse).Accept(after);

            CompileResult result = BlockCompiler.Compile(_zone);

            Assert.IsFalse(result.Success, "아니면 두 개가 컴파일에 성공함");
            Assert.AreEqual(Constants.CompilerMessages.DuplicateElse, result.Error);
            CollectionAssert.AreEqual(new[] { secondElse }, result.ErrorBlocks);
        }

        /// <summary>
        /// 시작하기 → 만약{ 개방하기 } → 완성하기 를 잇고 만약의 조건 슬롯을 반환한다 (조건은 아직 잇지 않는다).
        /// </summary>
        private ValueOutSocket BuildIfWithBody()
        {
            CodingBlock ifBlock = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.If, BlockCategory.FlowControl, _inventory);
            ValueOutSocket conditionSlot = BlockTestUtil.AddConditionSocket(ifBlock);
            InnerSocket inner = BlockTestUtil.AddInnerSocket(ifBlock);
            CodingBlock body = BlockTestUtil.MakeBlock(_zone, "개방하기", BlockCategory.Command, _inventory);

            ChainOutSocket.OfBlock(_start).Accept(ifBlock);
            inner.Accept(body);
            ChainOutSocket.OfBlock(ifBlock).Accept(_end);
            return conditionSlot;
        }

        /// <summary>
        /// 조건 블록을 인벤토리에 만든다.
        /// </summary>
        private CodingBlock MakeCondition(string label)
            => BlockTestUtil.MakeBlock(_zone, label, BlockCategory.Condition, _inventory);

        /// <summary>
        /// 그리고/또는 블록을 인벤토리에 만든다.
        /// </summary>
        private CodingBlock MakeLogic(string label)
            => BlockTestUtil.MakeBlock(_zone, label, BlockCategory.Logic, _inventory);
    }
}
