using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Scenes;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 스토리 줄 연출이 한 줄씩 아래에서 올라오며 페이드인되는지 검증한다.
    ///
    /// 배경: 수동 보간 루프를 DOVirtual.Float로 바꾸면서 스킵 검사용 .OnUpdate()를 덧붙였는데,
    /// DOVirtual.Float는 값 전달을 내부 OnUpdate로 구현하므로 보간 콜백이 덮어써져
    /// 줄이 올라오는 중간 과정 없이 끝에 한 번에 나타났다.
    /// </summary>
    public class StoryLineAnimatorTests
    {
        private GameObject _canvasGo;
        private TextMeshProUGUI _text;

        /// <summary>
        /// 두 줄짜리 테스트 텍스트를 만든다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _canvasGo = new GameObject("StoryLineAnimatorTests", typeof(Canvas));
            _canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(_canvasGo.transform, false);
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(800f, 400f);
            _text = textGo.AddComponent<TextMeshProUGUI>();
            _text.text = "First line\nSecond line";
        }

        /// <summary>
        /// 테스트용 캔버스를 파괴한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_canvasGo) UnityEngine.Object.DestroyImmediate(_canvasGo);
        }

        /// <summary>
        /// 첫 번째 글자 정점의 알파와 Y를 읽는다.
        /// </summary>
        private (byte alpha, float y) ReadFirstChar()
        {
            TMP_TextInfo info = _text.textInfo;
            TMP_CharacterInfo ch = info.characterInfo[0];
            TMP_MeshInfo mesh = info.meshInfo[ch.materialReferenceIndex];
            return (mesh.colors32[ch.vertexIndex].a, mesh.vertices[ch.vertexIndex].y);
        }

        /// <summary>
        /// 연출 도중에는 첫 줄이 반투명이면서 최종 위치보다 아래에 있어야 한다.
        /// </summary>
        [UnityTest]
        public IEnumerator 연출_도중의_줄은_반투명하고_아래에서_올라오는_중이다() => UniTask.ToCoroutine(async () =>
        {
            _text.ForceMeshUpdate();
            float finalY = ReadFirstChar().y;
            const float yOffset = 20f;

            using CancellationTokenSource cts = new CancellationTokenSource();
            UniTask anim = StoryLineAnimator.AnimateAsync(_text, 2f, 0.1f, yOffset, () => false, cts.Token);

            await UniTask.Delay(TimeSpan.FromSeconds(0.6f), DelayType.UnscaledDeltaTime);
            (byte alpha, float y) = ReadFirstChar();
            cts.Cancel();
            try { await anim.AwaitWithRealtimeTimeout(); } catch (OperationCanceledException) { }

            Assert.Greater(alpha, 0, "연출 도중인데 줄이 여전히 완전히 투명함 — 보간이 적용되지 않음");
            Assert.Less(alpha, 255, "연출 도중인데 줄이 이미 완전히 불투명함 — 한 번에 나타남");
            Assert.Less(y, finalY - 0.5f, "연출 도중인데 줄이 이미 최종 위치에 있음 — 아래에서 올라오지 않음");
        });

        /// <summary>
        /// 줄과 줄 사이를 기다리는 동안 들어온 스킵도 받아 남은 줄이 즉시 표시된다.
        /// 회귀: 줄 사이 대기를 고정 Delay로 기다려, 그 사이(연출 시간의 1/3쯤)에 누른 터치가 버려졌다.
        /// </summary>
        [UnityTest]
        public IEnumerator 줄_사이_대기_중에_스킵해도_남은_줄이_즉시_표시된다() => UniTask.ToCoroutine(async () =>
        {
            // 첫 줄은 곧바로 다 올라오고, 그 뒤 10초 대기 구간에서 스킵을 누른다
            bool skipNow = false;
            UniTask anim = StoryLineAnimator.AnimateAsync(_text, 0.05f, 10f, 20f, () => skipNow, CancellationToken.None);

            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), DelayType.UnscaledDeltaTime);
            skipNow = true;
            await anim.AwaitWithRealtimeTimeout();

            TMP_TextInfo info = _text.textInfo;
            TMP_CharacterInfo last = info.characterInfo[info.lineInfo[info.lineCount - 1].firstCharacterIndex];
            Assert.AreEqual(255, info.meshInfo[last.materialReferenceIndex].colors32[last.vertexIndex].a,
                "줄 사이 대기 중 스킵했는데 마지막 줄이 보이지 않음");
        });

        /// <summary>
        /// 스킵 입력이 들어오면 남은 줄 간격을 기다리지 않고 모든 줄이 즉시 표시된다.
        /// </summary>
        [UnityTest]
        public IEnumerator 스킵하면_모든_줄이_즉시_표시된다() => UniTask.ToCoroutine(async () =>
        {
            await StoryLineAnimator.AnimateAsync(_text, 10f, 10f, 20f, () => true, CancellationToken.None)
                .AwaitWithRealtimeTimeout();

            TMP_TextInfo info = _text.textInfo;
            for (int l = 0; l < info.lineCount; l++)
            {
                TMP_CharacterInfo ch = info.characterInfo[info.lineInfo[l].firstCharacterIndex];
                Assert.AreEqual(255, info.meshInfo[ch.materialReferenceIndex].colors32[ch.vertexIndex].a,
                    $"{l}번째 줄이 스킵 후에도 보이지 않음");
            }
        });
    }
}
