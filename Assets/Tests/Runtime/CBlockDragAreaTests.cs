#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// ㄷ자 블록(반복하기·만약·함수 정의)은 위쪽 라벨뿐 아니라 아래쪽 막대를 잡아도 블록 자신이 드래그되는지,
    /// 그 터치 영역이 안쪽·아래쪽에 붙은 블록의 터치를 가로채지 않는지 검증한다.
    /// 라벨이 터치를 받지 않는 일반 블록은 본체 스프라이트로 드래그되는지도 함께 확인한다.
    /// </summary>
    public class CBlockDragAreaTests
    {
        private const string PrefabFolder = "Assets/AddressableAssets/Prefabs/Blocks/";

        // 스프라이트의 하단 막대 높이 — Footer 위쪽부터 이 높이까지가 막대다
        private const float BarHeight = 101f;

        // 블록 스냅 트윈이 끝나고 Inner 높이가 맞춰질 때까지 기다리는 시간
        private const float SnapWaitSeconds = 1f;

        // 상단 헤더 위쪽 가장자리에서 이만큼 아래를 누른다 — 라벨 줄 안쪽
        private const float HeaderTouchDepth = 30f;

        private readonly static string[] CBlockPrefabs =
        {
            Constants.BlockAssets.WhilePrefab,
            Constants.BlockAssets.IfPrefab,
            Constants.BlockAssets.FuncDefPrefab
        };

        // 라벨 레이캐스트를 끄고 본체 스프라이트가 터치를 받는 일반 블록
        private readonly static string[] PlainBlockPrefabs =
        {
            Constants.BlockAssets.CommandPrefab,
            Constants.BlockAssets.CommandNoValuePrefab,
            Constants.BlockAssets.ConditionPrefab,
            Constants.BlockAssets.ValuePrefab,
            Constants.BlockAssets.StartPrefab,
            Constants.BlockAssets.EndPrefab,
            Constants.BlockAssets.FunctionPrefab
        };

        // 하단 막대가 없는 기타 흐름 블록까지 포함한 ㄷ자 블록 전체
        private readonly static string[] CBlockHeaderPrefabs =
        {
            Constants.BlockAssets.WhilePrefab,
            Constants.BlockAssets.IfPrefab,
            Constants.BlockAssets.FuncDefPrefab,
            Constants.BlockAssets.FlowControlPrefab
        };

        private UiRaycastStage _stage;
        private readonly List<RaycastResult> _results = new List<RaycastResult>();

        /// <summary>
        /// 레이캐스트에 필요한 EventSystem과 화면 오버레이 캔버스를 준비한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _stage = new UiRaycastStage(nameof(CBlockDragAreaTests));
        }

        /// <summary>
        /// 테스트가 만든 오브젝트를 모두 파괴한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _stage.Destroy();
        }

        /// <summary>
        /// 하단 막대의 왼쪽·가운데·오른쪽 어디를 눌러도 드래그 처리기가 그 ㄷ자 블록으로 잡힌다.
        /// </summary>
        [UnityTest]
        public IEnumerator 하단_막대를_누르면_ㄷ자_블록이_드래그된다([ValueSource(nameof(CBlockPrefabs))] string prefabName)
        {
            CodingBlock block = InstantiateBlock(prefabName);

            // 레이아웃 계산과 캔버스 그리기 순서(depth) 갱신을 기다린다
            yield return null;
            yield return null;

            RectTransform footer = (RectTransform)block.Footer;
            Rect rect = footer.rect;
            float barCenterY = rect.yMax - BarHeight * 0.5f;
            float[] xs = { rect.xMin + rect.width * 0.1f, rect.center.x, rect.xMin + rect.width * 0.8f };

            foreach (float x in xs)
                AssertDragTarget(block, footer, new Vector2(x, barCenterY), $"{prefabName} 하단 막대 (x={x:0})");
        }

        /// <summary>
        /// 상단 헤더(라벨 줄)를 누르면 드래그 처리기가 그 ㄷ자 블록으로 잡힌다 — 배경 이미지는 레이캐스트를 받지 않아 라벨이 헤더의 터치 영역이다.
        /// </summary>
        [UnityTest]
        public IEnumerator 상단_헤더를_누르면_ㄷ자_블록이_드래그된다([ValueSource(nameof(CBlockHeaderPrefabs))] string prefabName)
        {
            CodingBlock block = InstantiateBlock(prefabName);

            // 레이아웃 계산과 캔버스 그리기 순서(depth) 갱신을 기다린다
            yield return null;
            yield return null;

            RectTransform root = (RectTransform)block.transform;
            Rect rect = root.rect;
            AssertDragTarget(block, root, new Vector2(rect.xMin + rect.width * 0.1f, rect.yMax - HeaderTouchDepth), $"{prefabName} 상단 헤더");
        }

        /// <summary>
        /// 일반 블록은 라벨이 터치를 받지 않아도 본체 가운데를 누르면 드래그 처리기가 그 블록으로 잡힌다.
        /// </summary>
        [UnityTest]
        public IEnumerator 일반_블록은_본체를_누르면_드래그된다([ValueSource(nameof(PlainBlockPrefabs))] string prefabName)
        {
            CodingBlock block = InstantiateBlock(prefabName, null, BlockCategory.Command);

            // 레이아웃 계산과 캔버스 그리기 순서(depth) 갱신을 기다린다
            yield return null;
            yield return null;

            RectTransform root = (RectTransform)block.transform;
            AssertDragTarget(block, root, root.rect.center, $"{prefabName} 본체 가운데");
        }

        /// <summary>
        /// 반복하기 안쪽과 아래쪽에 블록을 붙여도 각 블록을 누르면 그 블록이, 하단 막대를 누르면 반복하기가 잡힌다.
        /// </summary>
        [UnityTest]
        public IEnumerator 하단_막대_터치_영역이_안쪽과_아래쪽_블록을_가리지_않는다()
        {
            CodingBlock repeat = InstantiateBlock(Constants.BlockAssets.WhilePrefab, Constants.BlockLabels.While, BlockCategory.FlowControl);
            CodingBlock inner = InstantiateBlock(Constants.BlockAssets.CommandNoValuePrefab, "안쪽 블록", BlockCategory.Command);
            CodingBlock below = InstantiateBlock(Constants.BlockAssets.CommandNoValuePrefab, "아래 블록", BlockCategory.Command);

            InnerSocket innerSocket = repeat.GetSocket<InnerSocket>();
            ChainOutSocket chainOut = ChainOutSocket.OfBlock(repeat);
            Assert.IsTrue(innerSocket, "반복하기에 InnerSocket 없음");
            Assert.IsTrue(chainOut, "반복하기에 ChainOutSocket 없음");
            innerSocket.Accept(inner);
            chainOut.Accept(below);

            // 앞 테스트가 timeScale을 되돌리지 못해도 멈추지 않도록 실시간으로 기다린다
            yield return new WaitForSecondsRealtime(SnapWaitSeconds);

            RectTransform footer = (RectTransform)repeat.Footer;
            AssertDragTarget(repeat, footer, new Vector2(footer.rect.center.x, footer.rect.yMax - BarHeight * 0.5f), "반복하기 하단 막대");

            // 안쪽 블록: 본체 가운데와 아래 가장자리 바로 위(하단 막대와 맞닿는 곳)
            RectTransform innerRt = (RectTransform)inner.transform;
            AssertDragTarget(inner, innerRt, innerRt.rect.center, "안쪽 블록 가운데");

            // 아래 블록: 본체 가운데와 위 가장자리 바로 아래(하단 막대와 맞닿는 곳)
            RectTransform belowRt = (RectTransform)below.transform;
            AssertDragTarget(below, belowRt, belowRt.rect.center, "아래 블록 가운데");
            AssertDragTarget(below, belowRt, new Vector2(belowRt.rect.center.x, belowRt.rect.yMax - 5f), "아래 블록 위 가장자리");
        }

        /// <summary>
        /// 블록 프리팹을 캔버스 아래에 인스턴스화한다. 라벨·카테고리를 주면 실제 생성 경로처럼 이름·메타·소켓을 맞춘다.
        /// </summary>
        private CodingBlock InstantiateBlock(string prefabName, string label = null, BlockCategory category = BlockCategory.FlowControl)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + prefabName + ".prefab");
            Assert.IsTrue(prefab, $"{prefabName} 프리팹을 찾지 못함");

            GameObject go = Object.Instantiate(prefab, _stage.Canvas.transform, false);
            Assert.IsTrue(go.TryGetComponent(out CodingBlock block), $"{prefabName}에 CodingBlock 없음");
            Assert.IsTrue(block.Footer || category != BlockCategory.FlowControl, $"{prefabName}의 CodingBlock에 Footer가 연결되지 않음");

            if (label != null)
            {
                go.name = label; // 소켓 오프셋이 블록 이름(라벨)으로 정해진다
                block.Init(category, _stage.Canvas);
                BlockFactory.AttachSockets(block);
            }

            return block;
        }

        /// <summary>
        /// 대상 RectTransform의 로컬 좌표 지점을 눌렀을 때 맨 위 UI의 드래그 처리기가 기대한 블록인지 확인한다.
        /// </summary>
        private void AssertDragTarget(CodingBlock expected, RectTransform space, Vector2 localPoint, string where)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, space.TransformPoint(localPoint));
            PointerEventData pointer = new PointerEventData(_stage.EventSystem) { position = screen };
            _results.Clear();
            _stage.EventSystem.RaycastAll(pointer, _results);

            Assert.IsNotEmpty(_results, $"{where} 위치에 맞은 UI가 없음");
            GameObject handler = ExecuteEvents.GetEventHandler<IBeginDragHandler>(_results[0].gameObject);
            Assert.AreSame(expected.gameObject, handler, $"{where}를 눌렀을 때 드래그 대상이 {(handler ? handler.name : "없음")}");
        }
    }
}
#endif
