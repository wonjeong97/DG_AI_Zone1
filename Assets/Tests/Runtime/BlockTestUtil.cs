using Data;
using Game;
using UnityEngine;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 프리팹 없이 코드로 최소 블록 그래프를 조립하는 테스트 헬퍼.
    /// 소켓은 실제 런타임과 같은 BlockFactory.AttachSockets로 붙여 소유 관계까지 동일하게 만든다.
    /// </summary>
    internal static class BlockTestUtil
    {
        /// <summary>
        /// 코딩 패널 역할을 할 CodingZone을 만든다.
        /// </summary>
        public static CodingZone MakeZone()
        {
            GameObject go = new GameObject("CodingZone", typeof(RectTransform));
            return go.AddComponent<CodingZone>();
        }

        /// <summary>
        /// 지정 카테고리의 블록을 만들어 부모 아래에 두고 소켓을 부착한 뒤 패널 목록에 등록한다.
        /// </summary>
        public static CodingBlock MakeBlock(CodingZone zone, string name, BlockCategory category, Transform parent,
            ValueKind valueKind = ValueKind.None, ControlRole role = ControlRole.None)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            CodingBlock block = go.AddComponent<CodingBlock>();
            block.Init(category, null, valueKind, role);
            block.SetZones(zone, null);
            BlockFactory.AttachSockets(block);
            zone.RegisterBlock(block);
            return block;
        }

        /// <summary>
        /// FlowControl 블록에 Inner 컨테이너 진입 소켓을 하나 붙여 소유로 등록한다.
        /// </summary>
        public static InnerSocket AddInnerSocket(CodingBlock flowBlock)
        {
            GameObject inner = new GameObject(Constants.BlockParts.InnerPrefix, typeof(RectTransform));
            inner.transform.SetParent(flowBlock.transform, false);
            GameObject socketGo = new GameObject(Constants.Sockets.InnerName, typeof(RectTransform));
            socketGo.transform.SetParent(inner.transform, false);
            InnerSocket socket = socketGo.AddComponent<InnerSocket>();
            flowBlock.RegisterSocket(socket);
            return socket;
        }

        /// <summary>
        /// 만약 블록 헤더에 내장된 조건 슬롯(ValueOutSocket)을 붙여 소유로 등록한다 — 프리팹에는 원래 들어 있는 소켓이다.
        /// </summary>
        public static ValueOutSocket AddConditionSocket(CodingBlock flowBlock)
        {
            GameObject socketGo = new GameObject("ConditionSlot", typeof(RectTransform));
            socketGo.transform.SetParent(flowBlock.transform, false);
            ValueOutSocket socket = socketGo.AddComponent<ValueOutSocket>();
            flowBlock.RegisterSocket(socket);
            return socket;
        }
    }
}
