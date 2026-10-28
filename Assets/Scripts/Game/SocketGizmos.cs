#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game
{
    // 여덟 개 소켓 스크립트가 거의 같은 코드를 각자 갖고 있던 것을 모았다.
    /// <summary>
    /// 소켓 기즈모(마커·스냅 반원·라벨)를 그리는 에디터 전용 공용 드로잉 유틸이다.
    /// </summary>
    internal static class SocketGizmos
    {
        private const int LabelFontSize = 9;

        // 기즈모는 씬 뷰가 그려질 때마다 호출되므로 스타일을 재사용한다
        // (도메인 리로드로 날아가면 다음 호출에서 다시 만들어진다)
        private static GUIStyle _labelStyle;

        /// <summary>
        /// 십자 마커와 중심 점을 그린다.
        /// </summary>
        public static void DrawCross(RectTransform rt, Color color, float arm, float dotRadius)
        {
            Gizmos.color = color;
            Vector2 pos = rt.position;
            Gizmos.DrawLine(pos + Vector2.left * arm, pos + Vector2.right * arm);
            Gizmos.DrawLine(pos + Vector2.up * arm, pos + Vector2.down * arm);
            Gizmos.DrawWireSphere(rt.position, dotRadius);
        }

        /// <summary>
        /// 가로선과 위로 뻗은 기둥을 그린다 — 블록 상단에 붙는 체인 In 소켓 전용 마커.
        /// </summary>
        public static void DrawStem(RectTransform rt, Color color, float arm, float dotRadius, float stemScale)
        {
            Gizmos.color = color;
            Vector2 pos = rt.position;
            Gizmos.DrawLine(pos + Vector2.left * arm, pos + Vector2.right * arm);
            Gizmos.DrawLine(pos, pos + Vector2.up * arm * stemScale);
            Gizmos.DrawWireSphere(rt.position, dotRadius);
        }

        /// <summary>
        /// 스냅 감지 범위를 from 방향에서 반시계 방향으로 angle도(기본 180도 반원) 그린다.
        /// </summary>
        public static void DrawSnapRange(RectTransform rt, Vector3 from, float radius, Color marker, Color range, float angle = 180f)
        {
            Handles.color = range;
            Handles.DrawSolidArc(rt.position, Vector3.forward, from, angle, radius);
            Handles.color = marker;
            Handles.DrawWireArc(rt.position, Vector3.forward, from, angle, radius);
        }

        /// <summary>
        /// 체인·안쪽 소켓의 스냅 감지 범위를 소켓 바로 아래를 가운데로 angle도 그린다 — 180도보다 넓으면 수평선 위까지 벌어진다.
        /// </summary>
        public static void DrawChainSnapRange(RectTransform rt, float radius, float angle, Color marker, Color range)
        {
            // 왼쪽에서 반시계로 돌면 아래를 지나 오른쪽에 닿으므로, 180도를 넘는 만큼의 반을 왼쪽 위로 당겨 시작한다
            Vector3 from = Quaternion.Euler(0f, 0f, -(angle - 180f) * 0.5f) * Vector3.left;
            DrawSnapRange(rt, from, radius, marker, range, angle);
        }

        /// <summary>
        /// 소켓 위에 색을 입힌 라벨을 그린다.
        /// </summary>
        public static void DrawLabel(RectTransform rt, string text, Color color, float yOffset)
        {
            _labelStyle ??= new GUIStyle { fontSize = LabelFontSize };
            _labelStyle.normal.textColor = color;   // 소켓마다 색이 달라 호출 시점에 덮어쓴다
            Handles.Label(rt.position + Vector3.up * yOffset, text, _labelStyle);
        }
    }
}
#endif
