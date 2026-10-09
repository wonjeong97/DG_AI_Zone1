using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 이미지를 늘리지 않고 바깥 가장자리 정점만 민다. 셰이더는 블록 사각형 기준 좌표에서 둘레를 샘플링하고
    // 9-slice 대응식으로 UV를 구하므로, 늘어난 칸 경계를 넘어도 테두리 두께가 같다.
    // TEXCOORD0: 사각형 기준 좌표(xy)·두께/사각형 크기(zw) / TEXCOORD1: 9-slice 칸 경계 / TEXCOORD2·3: 가로·세로 경계의 UV.
    /// <summary>
    /// 블록 외곽선·스냅 하이라이트 이미지의 메시를 테두리 두께만큼 밖으로 넓히고, Custom/UI/BlockOutline 셰이더가 쓸 정보를 정점에 싣는다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class BlockOutlineMesh : BaseMeshEffect
    {
        // 셰이더가 칸 경계(TEXCOORD1)와 경계 UV(TEXCOORD2·3)를 받으려면 캔버스가 이 채널을 메시에 실어야 한다
        private const AdditionalCanvasShaderChannels RequiredChannels =
            AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;

        // 축마다 경계는 최대 4개 — 양 끝과 9-slice 칸 경계 2개 (Simple은 양 끝 2개)
        private const int MaxBreaks = 4;
        private const float EdgeEpsilon = 0.01f;

        private readonly static List<UIVertex> _vertices = new();

        // 축별 경계 (x: 위치, y: 그 위치의 UV)
        private readonly static List<Vector2> _breaksX = new();
        private readonly static List<Vector2> _breaksY = new();

        /// <summary>
        /// 켜질 때 위쪽 캔버스들에 필요한 셰이더 채널을 켠다.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureCanvasChannels();
        }

        /// <summary>
        /// 드래그 등으로 다른 캔버스 아래로 옮겨져도 그 캔버스에 셰이더 채널을 켠다.
        /// </summary>
        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            EnsureCanvasChannels();
        }

        /// <summary>
        /// 이 이미지를 그리는 캔버스(중첩 캔버스 포함 위쪽 전부)에 칸 경계·경계 UV 채널을 켠다.
        /// </summary>
        private void EnsureCanvasChannels()
        {
            // 블록은 드래그 중 루트 캔버스로, 놓이면 코딩 패널 쪽 캔버스로 옮겨 다니므로 참조를 들고 있지 않고
            // 옮길 때마다 부모를 거슬러 올라가며 만나는 캔버스 전부에 채널을 켠다
            for (Transform t = transform; t; t = t.parent)
            {
                if (t.TryGetComponent(out Canvas canvas)
                    && (canvas.additionalShaderChannels & RequiredChannels) != RequiredChannels)
                    canvas.additionalShaderChannels |= RequiredChannels;
            }
        }

        /// <summary>
        /// 메시 바깥 경계에 닿은 정점을 테두리 두께만큼 밀고, 정점마다 사각형 기준 좌표·두께 비율·9-slice 정보를 싣는다.
        /// </summary>
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;

            int count = vh.currentVertCount;
            if (count == 0) return;

            _vertices.Clear();
            for (int i = 0; i < count; i++)
            {
                UIVertex v = default;
                vh.PopulateUIVertex(ref v, i);
                _vertices.Add(v);
            }

            // 넓히기 전 메시 경계 = 블록 사각형. 정점 격자에서 축마다 9-slice 칸 경계와 그 위치의 UV를 읽는다
            Vector2 min = _vertices[0].position;
            Vector2 max = min;
            _breaksX.Clear();
            _breaksY.Clear();
            foreach (UIVertex v in _vertices)
            {
                min = Vector2.Min(min, v.position);
                max = Vector2.Max(max, v.position);
                AddBreak(_breaksX, v.position.x, v.uv0.x);
                AddBreak(_breaksY, v.position.y, v.uv0.y);
            }

            Vector2 size = max - min;

            // 크기가 없거나 Tiled 등 9-slice보다 칸이 많은 메시는 이 셰이더가 다루지 않는다 — 원래 메시를 그대로 둔다
            if (size.x <= EdgeEpsilon || size.y <= EdgeEpsilon
                || _breaksX.Count > MaxBreaks || _breaksY.Count > MaxBreaks)
            {
                _vertices.Clear();
                return;
            }

            SortByPosition(_breaksX);
            SortByPosition(_breaksY);
            Vector2 sliceX = ToSlice(_breaksX, min.x, size.x, out Vector4 uvX);
            Vector2 sliceY = ToSlice(_breaksY, min.y, size.y, out Vector4 uvY);
            Vector4 slice = new Vector4(sliceX.x, sliceX.y, sliceY.x, sliceY.y);

            float thickness = Constants.HighlightSettings.OutlineThickness;
            Vector2 radius = new Vector2(thickness / size.x, thickness / size.y);

            for (int i = 0; i < count; i++)
            {
                UIVertex v = _vertices[i];
                Vector3 pos = v.position;

                if (pos.x <= min.x + EdgeEpsilon) pos.x -= thickness;
                else if (pos.x >= max.x - EdgeEpsilon) pos.x += thickness;

                if (pos.y <= min.y + EdgeEpsilon) pos.y -= thickness;
                else if (pos.y >= max.y - EdgeEpsilon) pos.y += thickness;

                v.position = pos;
                v.uv0 = new Vector4((pos.x - min.x) / size.x, (pos.y - min.y) / size.y, radius.x, radius.y);
                v.uv1 = slice;
                v.uv2 = uvX;
                v.uv3 = uvY;
                vh.SetUIVertex(v, i);
            }

            _vertices.Clear();
        }

        /// <summary>
        /// 처음 보는 위치면 (위치, UV) 경계를 추가한다.
        /// </summary>
        private static void AddBreak(List<Vector2> breaks, float position, float uv)
        {
            foreach (Vector2 b in breaks)
                if (Mathf.Abs(b.x - position) <= EdgeEpsilon) return;
            breaks.Add(new Vector2(position, uv));
        }

        /// <summary>
        /// 경계를 위치 순으로 정렬한다 — 펄스 중 매 프레임 불리고 4개 이하라 비교자 할당 없는 삽입 정렬로 충분하다.
        /// </summary>
        private static void SortByPosition(List<Vector2> breaks)
        {
            for (int i = 1; i < breaks.Count; i++)
            {
                Vector2 key = breaks[i];
                int j = i - 1;
                while (j >= 0 && breaks[j].x > key.x)
                {
                    breaks[j + 1] = breaks[j];
                    j--;
                }
                breaks[j + 1] = key;
            }
        }

        /// <summary>
        /// 정렬된 경계를 셰이더 형식으로 바꾼다 — 칸 경계 두 개(사각형 기준 좌표)와 0·경계·경계·1에서의 UV.
        /// </summary>
        private static Vector2 ToSlice(List<Vector2> breaks, float origin, float size, out Vector4 uv)
        {
            // 경계가 2개(Simple)나 3개(한쪽 테두리 폭 0)면 빈 칸을 끝으로 몰아 구간별 대응이 같게 한다.
            Vector2 first = breaks[0];
            Vector2 last = breaks[breaks.Count - 1];

            switch (breaks.Count)
            {
                case 2:
                    uv = new Vector4(first.y, first.y, last.y, last.y);
                    return new Vector2(0f, 1f);
                case 3:
                    Vector2 mid = breaks[1];
                    uv = new Vector4(first.y, mid.y, last.y, last.y);
                    return new Vector2((mid.x - origin) / size, 1f);
                default:
                    Vector2 lower = breaks[1];
                    Vector2 upper = breaks[2];
                    uv = new Vector4(first.y, lower.y, upper.y, last.y);
                    return new Vector2((lower.x - origin) / size, (upper.x - origin) / size);
            }
        }
    }
}
