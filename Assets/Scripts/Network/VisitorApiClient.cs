using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Data;
using HuliacDev.Utils;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.Networking;
using ZLogger;

namespace Network
{
    /// <summary>
    /// 체험자 서버(현장 내부망) API를 호출한다. 서버 주소는 StreamingAssets/Json/Server.json에서 호출마다 읽어
    /// 현장에서 파일만 고쳐도 다음 호출부터 반영된다.
    /// 템플릿 ApiRetryUtil은 응답 본문을 돌려주지 않고 에디터에서는 전송을 생략하는 로그 전송용이라 쓰지 않는다.
    /// uid에는 생년월일이 들어 있어 로그에 남기지 않는다.
    /// </summary>
    public class VisitorApiClient
    {
        private readonly static string SettingsPath =
            ZString.Concat(Constants.ResourcePaths.SceneSettingsFolder, "/", Constants.VisitorApi.SettingsFileName);

        private readonly ILogger<VisitorApiClient> _logger;

        /// <summary>
        /// 로거를 생성자 주입으로 받는다.
        /// </summary>
        public VisitorApiClient(ILogger<VisitorApiClient> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// uid로 체험 가능 여부를 서버에 묻는다. 요청이 실패해도 예외 대신 RequestFailed를 돌려주고, 취소만 예외로 전달한다.
        /// </summary>
        public async UniTask<CheckActiveResult> CheckActiveAsync(string uid, CancellationToken cancellationToken)
        {
            ServerSettings settings = await JsonLoader.LoadAsync<ServerSettings>(SettingsPath, cancellationToken, _logger);
            if (string.IsNullOrEmpty(settings.baseUrl))
            {
                if (_logger != null) _logger.ZLogError($"[VisitorApiClient] Server.json의 baseUrl이 비어 있어 체험자를 확인할 수 없습니다.");
                return CheckActiveResult.Failed();
            }

            string url = ZString.Concat(settings.baseUrl.TrimEnd('/'), Constants.VisitorApi.CheckActivePath, Uri.EscapeDataString(uid));
            string body = await GetTextAsync(url, settings.timeoutSeconds, cancellationToken);
            if (body == null) return CheckActiveResult.Failed();

            CheckActiveResult result = CheckActiveResult.Parse(body);
            if (result.Status == CheckActiveStatus.Unknown)
            {
                if (_logger != null) _logger.ZLogWarning($"[VisitorApiClient] checkActive 응답을 해석하지 못했습니다: '{body}'");
            }
            else if (_logger != null)
            {
                _logger.ZLogInformation($"[VisitorApiClient] checkActive 결과: {result.Status} (idx {result.IdxUser})");
            }

            return result;
        }

        /// <summary>
        /// GET 요청을 보내 응답 본문을 받는다. 연결 실패·시간 초과·HTTP 오류·잘못된 주소면 로그를 남기고 null을 돌려준다.
        /// </summary>
        private async UniTask<string> GetTextAsync(string url, int timeoutSeconds, CancellationToken cancellationToken)
        {
            try
            {
                using UnityWebRequest request = UnityWebRequest.Get(url);
                request.timeout = Mathf.Max(1, timeoutSeconds);

                // ToUniTask는 결과가 Success가 아니면 UnityWebRequestException을 던진다
                await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                return request.downloadHandler.text;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnityWebRequestException e)
            {
                if (_logger != null) _logger.ZLogWarning($"[VisitorApiClient] 서버 요청 실패 (HTTP {e.ResponseCode}): {e.Error}");
                return null;
            }
            catch (Exception e)
            {
                // Server.json 주소 형식이 잘못된 경우 등 — 타이틀이 QR 대기로 돌아갈 수 있도록 실패로 처리한다
                if (_logger != null) _logger.ZLogError($"[VisitorApiClient] 서버 요청 중 예외: {e.Message}");
                return null;
            }
        }
    }
}
