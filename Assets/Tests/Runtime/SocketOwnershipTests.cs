using Data;
using Game;
using NUnit.Framework;
using UnityEngine;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// GetComponentInParent/InChildren 대신 소켓 소유 관계로 판단하도록 바꾼 규칙들이
    /// 기존과 같은 결과를 내는지 검증한다 (제어 블록 위치 제한, 직속 소켓 조회).
    /// </summary>
    public class SocketOwnershipTests
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
        /// 코딩 패널에 따로 둔 체인(A→B)의 머리를 다른 체인(X→Y) 사이에 끼우면 체인째 들어가 X→A→B→Y가 된다.
        /// 회귀: 밀려난 Y를 들어온 체인의 꼬리가 아니라 머리 바로 아래에 붙여 X→A→Y→B로 순서가 섞였다.
        /// </summary>
        [Test]
        public void 체인을_다른_체인_사이에_끼우면_순서가_유지된다()
        {
            CodingBlock x = MakeCommand("X");
            CodingBlock y = MakeCommand("Y");
            CodingBlock a = MakeCommand("A");
            CodingBlock b = MakeCommand("B");
            ChainOutSocket.OfBlock(x).Accept(y);
            ChainOutSocket.OfBlock(a).Accept(b);

            ChainOutSocket.OfBlock(x).Accept(a);

            Assert.AreSame(a, ChainOutSocket.OfBlock(x).Occupant);
            Assert.AreSame(b, ChainOutSocket.OfBlock(a).Occupant, "들어온 체인의 둘째 블록이 떨어짐");
            Assert.AreSame(y, ChainOutSocket.OfBlock(b).Occupant, "밀려난 블록이 들어온 체인의 꼬리에 붙지 않음");
        }

        /// <summary>
        /// 두 블록 이상인 체인도 완성하기 바로 위에 끼울 수 있고, 완성하기는 체인 꼬리 아래로 밀려난다.
        /// </summary>
        [Test]
        public void 두_블록_체인도_완성하기_바로_위에_끼울_수_있다()
        {
            CodingBlock start = BlockTestUtil.MakeBlock(_zone, "시작하기", BlockCategory.Control, _zone.transform, role: ControlRole.Start);
            CodingBlock end = BlockTestUtil.MakeBlock(_zone, "완성하기", BlockCategory.Control, _zone.transform, role: ControlRole.End);
            CodingBlock a = MakeCommand("A");
            CodingBlock b = MakeCommand("B");
            ChainOutSocket.OfBlock(start).Accept(end);
            ChainOutSocket.OfBlock(a).Accept(b);

            Assert.IsTrue(ChainOutSocket.OfBlock(start).CanAccept(a), "두 블록 체인을 완성하기 위에 끼울 수 없음");
            ChainOutSocket.OfBlock(start).Accept(a);

            Assert.AreSame(end, ChainOutSocket.OfBlock(b).Occupant);
        }

        /// <summary>
        /// 반복하기 안 첫 자리에 체인을 끼워도 원래 있던 블록은 들어온 체인의 꼬리 아래로 간다.
        /// </summary>
        [Test]
        public void 내부_첫_자리에_체인을_끼우면_원래_블록은_꼬리_아래로_간다()
        {
            CodingBlock repeat = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.While, BlockCategory.FlowControl, _zone.transform);
            InnerSocket inner = BlockTestUtil.AddInnerSocket(repeat);
            CodingBlock y = MakeCommand("Y");
            CodingBlock a = MakeCommand("A");
            CodingBlock b = MakeCommand("B");
            inner.Accept(y);
            ChainOutSocket.OfBlock(a).Accept(b);

            inner.Accept(a);

            Assert.AreSame(a, inner.Occupant);
            Assert.AreSame(b, ChainOutSocket.OfBlock(a).Occupant);
            Assert.AreSame(y, ChainOutSocket.OfBlock(b).Occupant);
        }

        /// <summary>
        /// 코딩 패널에 값 슬롯 없는 움직이기 블록을 만든다.
        /// </summary>
        private CodingBlock MakeCommand(string label)
            => BlockTestUtil.MakeBlock(_zone, label, BlockCategory.Command, _zone.transform);

        /// <summary>
        /// 반복하기 내부에 들어간 블록은 여러 단계를 거쳐도 내부 컨테이너 안으로 판정된다.
        /// </summary>
        [Test]
        public void 내부_컨테이너_안의_블록은_중첩_단계와_무관하게_안쪽으로_판정된다()
        {
            CodingBlock repeat = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.While, BlockCategory.FlowControl, _zone.transform);
            InnerSocket inner = BlockTestUtil.AddInnerSocket(repeat);
            CodingBlock first = BlockTestUtil.MakeBlock(_zone, "개방하기", BlockCategory.Command, _zone.transform);
            CodingBlock second = BlockTestUtil.MakeBlock(_zone, "폐쇄하기", BlockCategory.Command, _zone.transform);

            inner.Accept(first);
            ChainOutSocket.OfBlock(first).Accept(second);

            Assert.IsTrue(first.IsInsideInnerContainer());
            Assert.IsTrue(second.IsInsideInnerContainer(), "체인으로 이어진 두 번째 블록도 내부로 판정돼야 함");
            Assert.IsFalse(repeat.IsInsideInnerContainer());
        }

        /// <summary>
        /// 내부 컨테이너 안의 체인 소켓은 완성하기 같은 제어 블록을 받지 않는다.
        /// </summary>
        [Test]
        public void 내부_컨테이너_안의_체인_소켓은_제어_블록을_거부한다()
        {
            CodingBlock start = BlockTestUtil.MakeBlock(_zone, "시작하기", BlockCategory.Control, _zone.transform, role: ControlRole.Start);
            CodingBlock repeat = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.While, BlockCategory.FlowControl, _zone.transform);
            InnerSocket inner = BlockTestUtil.AddInnerSocket(repeat);
            CodingBlock cmd = BlockTestUtil.MakeBlock(_zone, "개방하기", BlockCategory.Command, _zone.transform);
            CodingBlock end = BlockTestUtil.MakeBlock(_zone, "완성하기", BlockCategory.Control, _zone.transform, role: ControlRole.End);

            ChainOutSocket.OfBlock(start).Accept(repeat);
            inner.Accept(cmd);

            Assert.IsFalse(ChainOutSocket.OfBlock(cmd).CanAccept(end), "내부 체인 끝에 완성하기가 붙으면 안 됨");
            Assert.IsTrue(ChainOutSocket.OfBlock(repeat).CanAccept(end), "메인 체인에는 완성하기가 붙어야 함");
            Assert.IsFalse(inner.CanAccept(end), "Inner 소켓은 제어 블록을 받지 않아야 함");
        }

        /// <summary>
        /// Inner 소켓의 수락 판정은 드래그 중 매 프레임 호출되므로 힙 할당이 없어야 한다
        /// (중첩된 반복하기가 있어 재귀 판정을 거치는 경우 포함).
        /// </summary>
        [Test]
        public void Inner_소켓_수락_판정은_힙_할당이_없다()
        {
            CodingBlock outer = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.While, BlockCategory.FlowControl, _zone.transform);
            InnerSocket outerInner = BlockTestUtil.AddInnerSocket(outer);
            CodingBlock nested = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.While, BlockCategory.FlowControl, _zone.transform);
            BlockTestUtil.AddInnerSocket(nested);
            CodingBlock cmd = BlockTestUtil.MakeBlock(_zone, "개방하기", BlockCategory.Command, _zone.transform);

            // 드래그 중인 블록(nested) 안에 또 명령이 들어 있어 재귀 판정을 탄다
            nested.GetSocket<InnerSocket>().Accept(cmd);
            CodingBlock target = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.While, BlockCategory.FlowControl, _zone.transform);
            InnerSocket targetInner = BlockTestUtil.AddInnerSocket(target);

            targetInner.CanAccept(nested); // JIT 등 최초 호출 비용 제외
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) targetInner.CanAccept(nested);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "드래그 중 매 프레임 호출되는 수락 판정에서 힙 할당이 발생함");
            Assert.IsTrue(outerInner.CanAccept(nested), "제어 블록이 없는 체인은 받아야 함");
        }

        /// <summary>
        /// 블록의 직속 체인 소켓은 내부 컨테이너 안 블록의 체인 소켓과 섞이지 않는다.
        /// </summary>
        [Test]
        public void 직속_체인_소켓은_내부_블록의_소켓과_섞이지_않는다()
        {
            CodingBlock repeat = BlockTestUtil.MakeBlock(_zone, Constants.BlockLabels.While, BlockCategory.FlowControl, _zone.transform);
            InnerSocket inner = BlockTestUtil.AddInnerSocket(repeat);
            CodingBlock cmd = BlockTestUtil.MakeBlock(_zone, "개방하기", BlockCategory.Command, _zone.transform);
            inner.Accept(cmd);

            ChainOutSocket repeatOut = ChainOutSocket.OfBlock(repeat);
            ChainOutSocket cmdOut = ChainOutSocket.OfBlock(cmd);

            Assert.IsNotNull(repeatOut);
            Assert.AreNotSame(repeatOut, cmdOut);
            Assert.AreSame(repeat, repeatOut.Owner);
            Assert.AreSame(cmd, cmdOut.Owner);
            Assert.AreSame(repeat, inner.Owner);
        }
    }
}
