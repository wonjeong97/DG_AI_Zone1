using UnityEngine;

namespace Game
{
    // Command 블록의 값 연결 포인트. Value 블록의 ValueInSocket과 위치를 맞춰 스냅한다.
    public class ValueOutSocket : BlockSocket
    {
        /// <summary>
        /// 값 블록을 점유로 기록하고 ValueInSocket이 이 소켓에 맞도록 스냅시킨다.
        /// </summary>
        public override void Accept(CodingBlock block)
        {
            SetOccupant(block);
            ValueInSocket inSocket = FindChildComponent<ValueInSocket>(block.transform, Constants.Sockets.ValueInName);
            block.SnapInto(transform, ComputeSnapOffset(block, inSocket)).Forget();
        }

#if UNITY_EDITOR
        // 기즈모로 그리는 기준 반경 — 실제 스냅 반경은 3_Game.json(snapRadius·chainSnapRadius) × 코딩 패널 배율이라 다를 수 있다
        private const float SnapRadius = 120f;

        /// <summary>
        /// 씬 뷰에 소켓 위치와 스냅 범위를 표시한다.
        /// </summary>
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
