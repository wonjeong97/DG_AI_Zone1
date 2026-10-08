using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Network;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 레벨 결과를 서버에 올릴 때 쓰는 이 존(1존)의 콘텐츠 코드와 저장 응답 판정, 요청 취소 전달을 검증한다.
    /// </summary>
    public class VisitorApiClientTests
    {
        /// <summary>
        /// 레벨 순번(0부터)이 A1~A5로 바뀐다 — 레벨1은 A1, 레벨5는 A5.
        /// </summary>
        [TestCase(0, "A1")]
        [TestCase(1, "A2")]
        [TestCase(2, "A3")]
        [TestCase(3, "A4")]
        [TestCase(4, "A5")]
        public void 레벨_순번은_1존_콘텐츠_코드로_바뀐다(int levelIndex, string expected)
        {
            Assert.AreEqual(expected, VisitorApiClient.GetLevelCode(levelIndex));
        }

        /// <summary>
        /// updateValue 응답의 result가 true면 저장 성공이다 — 응답 예시는 현장 서버 실측값이다.
        /// </summary>
        [TestCase("{\n    \"result\":true,\n    \"idx_user\":8,\n    \"code\":\"A1\",\n    \"value\":0\n}")]
        [TestCase("\r\n{\"result\":true,\"idx_user\":10,\"code\":\"A5\",\"value\":1}\r\n")]
        public void 결과_저장_응답의_result가_true면_성공이다(string body)
        {
            Assert.IsTrue(UpdateValueResponse.IsSaved(body));
        }

        /// <summary>
        /// result가 false이거나 JSON이 아닌 응답은 저장 실패로 본다.
        /// </summary>
        [TestCase("{\"result\":false,\"message\":\"ERROR_IDX_USER\"}")]
        [TestCase("{\"result\":false,\"message\":\"NOT_FOUND\"}")]
        [TestCase("{}")]
        [TestCase("OK")]
        [TestCase("<html><body>Error</body></html>")]
        [TestCase("")]
        [TestCase(null)]
        public void 결과_저장_실패_응답은_실패로_본다(string body)
        {
            Assert.IsFalse(UpdateValueResponse.IsSaved(body));
        }

        /// <summary>
        /// JSON 뒤에 붙은 글자(현장 서버 getUser에서 본 ``` 줄 등)는 무시하고 result로 판정한다.
        /// </summary>
        [TestCase("\r\n{\"result\":true,\"idx_user\":13,\"code\":\"A1\",\"value\":1}\r\n```\r\n", true)]
        [TestCase("{\"result\":false,\"message\":\"ERROR_IDX_USER\"}\r\n```", false)]
        public void 결과_저장_응답_뒤에_붙은_글자는_무시한다(string body, bool expected)
        {
            Assert.AreEqual(expected, UpdateValueResponse.IsSaved(body));
        }

        /// <summary>
        /// 취소된 요청은 Server.json을 기본값(빈 baseUrl)으로 읽어도 '서버 주소 없음' 실패로 끝나지 않고 취소로 전달된다
        /// (JsonLoader는 취소돼도 예외 대신 기본값을 돌려준다).
        /// </summary>
        [UnityTest]
        public IEnumerator 취소된_요청은_서버_주소_없음이_아니라_취소로_전달된다() => UniTask.ToCoroutine(async () =>
        {
            VisitorApiClient client = new VisitorApiClient(null);
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            bool canceled = false;
            try
            {
                await client.CheckActiveAsync("TEST", cts.Token);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            Assert.IsTrue(canceled, "취소가 예외로 전달되지 않아 'baseUrl이 비어 있어' 에러를 남기고 실패로 끝남");
        });
    }
}
