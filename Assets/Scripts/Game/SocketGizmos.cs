#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 소켓 기즈모(마커·스냅 반원·라벨) 공용 드로잉.
    /// 여덟 개 소켓 스크립트가 거의 같은 코드를 각자 갖고 있던 것을 모았다. 에디터 전용.
    /// </summary>
    internal static class SocketGizmos
    {
        private const int LabelFontSize = 9;

        // 십자 마커 + 중심 점
        public static void DrawCross(RectTransform rt, Color color, float arm, float dotRadius)
        {
            Gizmos.color = color;
            Vector2 pos = rt.position;
            Gizmos.DrawLine(pos + Vector2.left * arm, pos + Vector2.right * arm);
            Gizmos.DrawLine(pos + Vector2.up * arm, pos + Vector2.down * arm);
            Gizmos.DrawWireSphere(rt.position, dotRadius);
        }

        // 가로선 + 위로 뻗은 기둥 — 블록 상단에 붙는 체인 In 소켓 전용 마커
        public static void DrawStem(RectTransform rt, Color color, float arm, float dotRadius, float stemScale)
        {
            Gizmos.color = color;
            Vector2 pos = rt.position;
            Gizmos.DrawLine(pos + Vector2.left * arm, pos + Vector2.right * arm);
            Gizmos.DrawLine(pos, pos + Vector2.up * arm * stemScale);
            Gizmos.DrawWireSphere(rt.position, dotRadius);
        }

        // 스냅 감지 범위 — from 방향에서 시계 방향으로 180도(반원)
        public static void DrawSnapRange(RectTransform rt, Vector3 from, float radius, Color marker, Color range)
        {
            Handles.color = range;
            Handles.DrawSolidArc(rt.position, Vector3.forward, from, 180f, radius);
            Handles.color = marker;
            Handles.DrawWireArc(rt.position, Vector3.forward, from, 180f, radius);
        }

        public static void DrawLabel(RectTransform rt, string text, Color color, float yOffset)
        {
            Handles.Label(rt.position + Vector3.up * yOffset, text,
                new GUIStyle { normal = { textColor = color }, fontSize = LabelFontSize });
        }
    }
}
#endif
