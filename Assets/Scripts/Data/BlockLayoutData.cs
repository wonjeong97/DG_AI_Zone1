using System;
using DG.Game;
using UnityEngine;

namespace DG.Data
{
    [CreateAssetMenu(fileName = "BlockLayoutData", menuName = "DG/Block Layout Data")]
    public class BlockLayoutData : ScriptableObject
    {
        [Tooltip("좌측 인벤토리에 배치될 블록들")]
        public BlockEntry[] inventoryBlocks;

        [Tooltip("우측 코딩 영역에 초기 배치될 블록들 (시작하기/종료하기 사이)")]
        public BlockEntry[] codingBlocks;
    }

    [Serializable]
    public class BlockEntry
    {
        public BlockCategory category;
        public string label;

        [Tooltip("Command 블록의 우측 값. 비어있으면 빈 ValueSlot")]
        public string valueLabel;

        // SerializeReference: 재귀 타입의 깊이 제한 우회
        [SerializeReference, Tooltip("FlowControl(만약/반복하기) 내부 블록")]
        public BlockEntry[] innerBlocks;

        [SerializeReference, Tooltip("만약 블록의 else 내부 블록")]
        public BlockEntry[] elseBlocks;

        [SerializeReference, Tooltip("Logic(그리고) 수평 체인 블록")]
        public BlockEntry[] chainBlocks;

        [Tooltip("코딩 존 진입 시 ChainSocket의 앵커(하단 중앙) 기준 오프셋")]
        public Vector2 chainSocketOffset = Vector2.zero;
    }
}
