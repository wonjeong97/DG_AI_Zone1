using UnityEngine;

namespace Game
{
    // 조건 블록의 우측 연결 포인트. 다음 조건/Logic 블록의 ConditionInSocket과 위치를 맞춰 스냅한다.
    public class ConditionOutSocket : BlockSocket
    {
        public void Accept(CodingBlock block)
        {
            SetOccupant(block);
            block.SnapInto(transform, ComputeSnapOffset(block, block.GetComponentInChildren<ConditionInSocket>())).Forget();
        }

#if UNITY_EDITOR
        private const float SnapRadius = 120f;

        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out RectTransform rt)) return;

            Color markerColor = IsEmpty ? new Color(0.6f, 0.3f, 1f, 0.9f) : new Color(1f, 0.4f, 0.1f, 0.9f);

            SocketGizmos.DrawCross(rt, markerColor, arm: 10f, dotRadius: 4f);
            // 스냅 감지 범위 — 1·4사분면(우측 반원). 범위 색은 점유 여부와 무관하게 고정
            SocketGizmos.DrawSnapRange(rt, Vector3.down, SnapRadius, markerColor, new Color(0.6f, 0.3f, 1f, 0.08f));
            SocketGizmos.DrawLabel(rt, $"CondOut  r={SnapRadius}", new Color(0.6f, 0.3f, 1f), yOffset: 16f);
        }
#endif
    }
}
