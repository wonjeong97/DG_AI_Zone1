using UnityEngine;

namespace Game
{
    // Command 블록의 값 연결 포인트. Value 블록의 ValueInSocket과 위치를 맞춰 스냅한다.
    public class ValueOutSocket : BlockSocket
    {
        public void Accept(CodingBlock block)
        {
            SetOccupant(block);
            block.SnapInto(transform, ComputeSnapOffset(block, block.GetComponentInChildren<ValueInSocket>())).Forget();
        }

#if UNITY_EDITOR
        private const float SnapRadius = 120f;

        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out RectTransform rt)) return;

            Color markerColor = IsEmpty ? new Color(1f, 0.85f, 0f, 0.9f)  : new Color(1f, 0.4f, 0.1f, 0.9f);
            Color rangeColor  = IsEmpty ? new Color(1f, 0.85f, 0f, 0.08f) : new Color(1f, 0.4f, 0.1f, 0.08f);

            SocketGizmos.DrawCross(rt, markerColor, arm: 10f, dotRadius: 4f);
            // 스냅 감지 범위 — 1·4사분면(우측 반원)
            SocketGizmos.DrawSnapRange(rt, Vector3.down, SnapRadius, markerColor, rangeColor);
            SocketGizmos.DrawLabel(rt, $"ValueOut  r={SnapRadius}", new Color(1f, 0.85f, 0f), yOffset: 16f);
        }
#endif
    }
}
