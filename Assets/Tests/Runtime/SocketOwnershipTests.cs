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
            CodingBlock.RestrictMainChainToFunction = false;
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
