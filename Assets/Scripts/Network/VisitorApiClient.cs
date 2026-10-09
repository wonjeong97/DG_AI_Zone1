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
    /// 체험자 서버(현장 내부망) API를 호출한다. 서버 주소·시간 초과·재시도 횟수는 StreamingAssets/Json/Server.json에서
    /// 호출마다 읽어 현장에서 파일만 고쳐도 다음 호출부터 반영된다. 네트워크가 불안정할 수 있어 요청이 실패하면 다시 시도한다.
    /// 템플릿 ApiRetryUtil은 응답 본문을 돌려주지 않고 에디터에서는 전송을 생략하는 로그 전송용이라 쓰지 않는다.
    /// uid에는 생년월일이 들어 있어 로그에 남기지 않는다.
    /// </summary>
    public class VisitorApiClient
    {
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
            ServerSettings settings = await LoadSettingsAsync(cancellationToken);
            if (settings == null) return CheckActiveResult.Failed();

            string url = ZString.Concat(settings.baseUrl.TrimEnd('/'), Constants.VisitorApi.CheckActivePath, Uri.EscapeDataString(uid));
            string body = await GetTextAsync(url, settings.qrCheckTimeoutSeconds, settings.qrCheckMaxAttempts, settings.retryDelaySeconds,
                "checkActive", cancellationToken);
            if (body == null) return CheckActiveResult.Failed();

            CheckActiveResult result = CheckActiveResult.Parse(body);
            if (result.Status == CheckActiveStatus.Unknown)
            {
                // 서버 오류 페이지가 요청 주소(uid)를 되돌려 보여 줄 수 있어 원문은 남기지 않는다
                if (_logger != null) _logger.ZLogWarning($"[VisitorApiClient] checkActive 응답을 해석하지 못했습니다 (응답 {(body == null ? 0 : body.Length)}자).");
            }
            else if (_logger != null)
            {
                _logger.ZLogInformation($"[VisitorApiClient] checkActive 결과: {result.Status} (idx {result.IdxUser})");
            }

            return result;
        }

        /// <summary>
        /// uid로 체험자 진행도(이 존의 레벨 기록)를 서버에서 받는다. 요청이 실패하거나 체험자가 없다는 응답이면 IsFound가 false이고,
        /// 취소만 예외로 전달한다. 응답에 uid·이름이 들어 있어 원문은 로그에 남기지 않는다.
        /// </summary>
        public async UniTask<GetUserResult> GetUserAsync(string uid, CancellationToken cancellationToken)
        {
            ServerSettings settings = await LoadSettingsAsync(cancellationToken);
            if (settings == null) return GetUserResult.Failed("서버 주소 없음");

            string url = ZString.Concat(settings.baseUrl.TrimEnd('/'), Constants.VisitorApi.GetUserPath, Uri.EscapeDataString(uid));
            string body = await GetTextAsync(url, settings.qrCheckTimeoutSeconds, settings.qrCheckMaxAttempts, settings.retryDelaySeconds,
                "getUser", cancellationToken);
            if (body == null) return GetUserResult.Failed("요청 실패");

            GetUserResult result = GetUserResult.Parse(body);
            if (!result.IsFound)
            {
                if (_logger != null) _logger.ZLogWarning($"[VisitorApiClient] getUser 응답에서 진행도를 읽지 못했습니다: {result.FailReason}");
            }
            else if (_logger != null)
            {
                _logger.ZLogInformation($"[VisitorApiClient] getUser 결과: 기록 있는 마지막 레벨 순번 {result.LastRecordedLevelIndex}");
            }

            return result;
        }

        /// <summary>
        /// 레벨 순번(0부터)을 이 존의 콘텐츠 코드로 바꾼다 — 레벨1은 A1, 레벨5는 A5.
        /// </summary>
        public static string GetLevelCode(int levelIndex) => ZString.Concat(Constants.VisitorApi.ZoneCode, levelIndex + 1);

        /// <summary>
        /// 체험자의 레벨 결과(성공 1·실패 0)를 서버에 올린다. 응답 JSON의 result가 true일 때만 저장된 것으로 본다.
        /// 요청이 실패하거나 서버가 저장하지 않았다고 응답하면 로그만 남기고 false를 돌려주며, 취소만 예외로 전달한다.
        /// visitorName은 서버에 보내지 않고 누구의 결과인지 로그에 남기는 데만 쓴다.
        /// </summary>
        public async UniTask<bool> UpdateValueAsync(int idxUser, string visitorName, string code, bool isSuccess,
            CancellationToken cancellationToken)
        {
            ServerSettings settings = await LoadSettingsAsync(cancellationToken);
            if (settings == null) return false;

            int value = isSuccess ? 1 : 0;
            string path = ZString.Format(Constants.VisitorApi.UpdateValuePathFormat, idxUser, Uri.EscapeDataString(code), value);
            string body = await GetTextAsync(ZString.Concat(settings.baseUrl.TrimEnd('/'), path), settings.uploadTimeoutSeconds, settings.uploadMaxAttempts,
                settings.retryDelaySeconds, "updateValue", cancellationToken);
            if (body == null)
            {
                if (_logger != null) _logger.ZLogError($"[VisitorApiClient] 레벨 결과를 올리지 못했습니다 (idx {idxUser}, 이름 {visitorName}, {code}={value}).");
                return false;
            }

            if (!UpdateValueResponse.IsSaved(body))
            {
                if (_logger != null) _logger.ZLogError($"[VisitorApiClient] 서버가 레벨 결과를 저장하지 않았습니다 (idx {idxUser}, 이름 {visitorName}, {code}={value}), 응답: '{body.Trim()}'");
                return false;
            }

            if (_logger != null) _logger.ZLogInformation($"[VisitorApiClient] 레벨 결과 저장 완료 (idx {idxUser}, 이름 {visitorName}, {code}={value})");
            return true;
        }

        /// <summary>
        /// Server.json을 읽는다. baseUrl이 비어 있으면 로그를 남기고 null을 돌려주며, 취소는 예외로 전달한다.
        /// </summary>
        private async UniTask<ServerSettings> LoadSettingsAsync(CancellationToken cancellationToken)
        {
            ServerSettings settings = await JsonLoader.LoadAsync<ServerSettings>(Constants.SettingsFiles.Server, cancellationToken, _logger);

            // JsonLoader는 취소돼도 기본값(빈 baseUrl)을 돌려주므로 여기서 취소를 전달한다 — 취소 뒤 'baseUrl이 비어 있어' 에러가 남지 않게
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(settings.baseUrl)) return settings;

            if (_logger != null) _logger.ZLogError($"[VisitorApiClient] Server.json의 baseUrl이 비어 있어 서버를 호출할 수 없습니다.");
            return null;
        }

        /// <summary>
        /// GET 요청을 보내 응답 본문을 받는다. 연결 실패·시간 초과·HTTP 오류·잘못된 주소면 재시도 간격을 두고
        /// 최대 시도 횟수까지 다시 보내며, 모두 실패하면 로그를 남기고 null을 돌려준다.
        /// 서버가 답한 결과(NOT_FOUND 등)는 다시 보내도 같으므로 재시도하지 않는다 — 응답 본문 해석은 호출부가 한다.
        /// </summary>
        private async UniTask<string> GetTextAsync(string url, int timeoutSeconds, int maxAttempts, float retryDelaySeconds,
            string apiName, CancellationToken cancellationToken)
        {
            maxAttempts = Mathf.Max(1, maxAttempts);

            // 재시도 대기는 Time.timeScale과 무관해야 한다 — 일시정지 중에도 재시도가 멈추지 않게
            TimeSpan retryDelay = TimeSpan.FromSeconds(Mathf.Max(0f, retryDelaySeconds));

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                string body = await SendGetAsync(url, timeoutSeconds, apiName, attempt, maxAttempts, cancellationToken);
                if (body != null) return body;

                if (attempt < maxAttempts)
                    await UniTask.Delay(retryDelay, DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
            }

            if (_logger != null) _logger.ZLogError($"[VisitorApiClient] {apiName} 요청이 {maxAttempts}번 모두 실패했습니다.");
            return null;
        }

        /// <summary>
        /// GET 요청을 한 번 보내 응답 본문을 받는다. 실패하면 몇 번째 시도인지와 함께 경고를 남기고 null을 돌려준다.
        /// </summary>
        private async UniTask<string> SendGetAsync(string url, int timeoutSeconds, string apiName, int attempt, int maxAttempts,
            CancellationToken cancellationToken)
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
                if (_logger != null) _logger.ZLogWarning($"[VisitorApiClient] {apiName} 요청 실패 ({attempt}/{maxAttempts}, HTTP {e.ResponseCode}): {e.Error}");
                return null;
            }
            catch (Exception e)
            {
                // Server.json 주소 형식이 잘못된 경우 등 — 타이틀이 QR 대기로 돌아갈 수 있도록 실패로 처리한다
                if (_logger != null) _logger.ZLogWarning($"[VisitorApiClient] {apiName} 요청 중 예외 ({attempt}/{maxAttempts}): {e.Message}");
                return null;
            }
        }
    }
}
