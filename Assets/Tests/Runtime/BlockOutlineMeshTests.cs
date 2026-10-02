using Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 외곽선 메시가 이미지를 늘리지 않고 바깥 경계만 테두리 두께만큼 넓히고, 셰이더가 쓸 사각형·9-slice 정보를 싣는지 검증한다.
    /// 이미지를 통째로 늘리면 블록 연결부(홈·돌기)에서 외곽선이 본체와 어긋난다.
    /// </summary>
    public class BlockOutlineMeshTests
    {
        private const float Tolerance = 1e-5f;

        private GameObject _go;
        private BlockOutlineMesh _effect;

        private static float Thickness => Constants.HighlightSettings.OutlineThickness;

        /// <summary>
        /// 외곽선 이미지에 메시 효과를 붙인다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SpriteOutline", typeof(RectTransform), typeof(Image));
            _effect = _go.AddComponent<BlockOutlineMesh>();
        }

        /// <summary>
        /// 테스트가 만든 오브젝트를 파괴한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
        }

        /// <summary>
        /// Image와 같은 순서(좌하 → 좌상 → 우상 → 우하)로 사각형 한 칸을 넣는다.
        /// </summary>
        private static void AddQuad(VertexHelper vh, Vector2 posMin, Vector2 posMax, Vector2 uvMin, Vector2 uvMax)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(posMin.x, posMin.y), Color.white, new Vector2(uvMin.x, uvMin.y));
            vh.AddVert(new Vector3(posMin.x, posMax.y), Color.white, new Vector2(uvMin.x, uvMax.y));
            vh.AddVert(new Vector3(posMax.x, posMax.y), Color.white, new Vector2(uvMax.x, uvMax.y));
            vh.AddVert(new Vector3(posMax.x, posMin.y), Color.white, new Vector2(uvMax.x, uvMin.y));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        /// <summary>
        /// i번째 정점을 읽는다.
        /// </summary>
        private static UIVertex GetVertex(VertexHelper vh, int i)
        {
            UIVertex v = default;
            vh.PopulateUIVertex(ref v, i);
            return v;
        }

        /// <summary>
        /// 한 칸 이미지는 사방으로 두께만큼 넓히고, 사각형 기준 좌표가 여백까지 이어지며 칸 경계는 양 끝뿐이다.
        /// </summary>
        [Test]
        public void 한_칸_이미지는_사방으로_두께만큼_넓히고_사각형_기준_좌표를_싣는다()
        {
            using VertexHelper vh = new VertexHelper();
            AddQuad(vh, Vector2.zero, new Vector2(100f, 50f), Vector2.zero, Vector2.one);

            _effect.ModifyMesh(vh);

            UIVertex bottomLeft = GetVertex(vh, 0);
            UIVertex topRight = GetVertex(vh, 2);
            Assert.AreEqual(-Thickness, bottomLeft.position.x, Tolerance);
            Assert.AreEqual(-Thickness, bottomLeft.position.y, Tolerance);
            Assert.AreEqual(100f + Thickness, topRight.position.x, Tolerance);
            Assert.AreEqual(50f + Thickness, topRight.position.y, Tolerance);

            // 사각형 기준 좌표(0~1)는 여백에서 범위를 벗어나고, zw는 두께/사각형 크기
            Assert.AreEqual(-Thickness / 100f, bottomLeft.uv0.x, Tolerance);
            Assert.AreEqual(-Thickness / 50f, bottomLeft.uv0.y, Tolerance);
            Assert.AreEqual(1f + Thickness / 100f, topRight.uv0.x, Tolerance);
            Assert.AreEqual(1f + Thickness / 50f, topRight.uv0.y, Tolerance);
            Assert.AreEqual(Thickness / 100f, bottomLeft.uv0.z, Tolerance);
            Assert.AreEqual(Thickness / 50f, bottomLeft.uv0.w, Tolerance);

            // 칸 경계는 양 끝뿐 — 가로·세로 모두 0~1을 그대로 대응한다
            Assert.AreEqual(new Vector4(0f, 1f, 0f, 1f), bottomLeft.uv1);
            Assert.AreEqual(new Vector4(0f, 0f, 1f, 1f), bottomLeft.uv2);
            Assert.AreEqual(new Vector4(0f, 0f, 1f, 1f), bottomLeft.uv3);
        }

        /// <summary>
        /// 세로 9-slice(아래 테두리·늘어난 가운데·위 테두리)는 바깥 경계만 넓히고, 칸 경계와 그 위치의 UV를 싣는다.
        /// </summary>
        [Test]
        public void 세로_9슬라이스는_바깥_경계만_넓히고_칸_경계와_UV를_싣는다()
        {
            using VertexHelper vh = new VertexHelper();
            // 아래 테두리(높이 20 = UV 0.25) / 가운데(높이 200으로 늘어남 = UV 0.5) / 위 테두리(높이 20 = UV 0.25)
            AddQuad(vh, Vector2.zero, new Vector2(100f, 20f), Vector2.zero, new Vector2(1f, 0.25f));
            AddQuad(vh, new Vector2(0f, 20f), new Vector2(100f, 220f), new Vector2(0f, 0.25f), new Vector2(1f, 0.75f));
            AddQuad(vh, new Vector2(0f, 220f), new Vector2(100f, 240f), new Vector2(0f, 0.75f), Vector2.one);

            _effect.ModifyMesh(vh);

            UIVertex innerCorner = GetVertex(vh, 1); // 아래 칸의 좌상 — 칸 경계라 세로로는 그대로
            UIVertex outerTop = GetVertex(vh, 9);    // 위 칸의 좌상 — 바깥 경계라 넓힘
            Assert.AreEqual(20f, innerCorner.position.y, Tolerance);
            Assert.AreEqual(20f / 240f, innerCorner.uv0.y, Tolerance);
            Assert.AreEqual(240f + Thickness, outerTop.position.y, Tolerance);
            Assert.AreEqual((240f + Thickness) / 240f, outerTop.uv0.y, Tolerance);

            // 세로 칸 경계(사각형 기준)와 0·경계·경계·1에서의 UV
            Assert.AreEqual(20f / 240f, outerTop.uv1.z, Tolerance);
            Assert.AreEqual(220f / 240f, outerTop.uv1.w, Tolerance);
            Assert.AreEqual(new Vector4(0f, 0.25f, 0.75f, 1f), outerTop.uv3);
        }
    }
}
