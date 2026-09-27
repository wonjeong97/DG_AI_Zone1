using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Data;
using Microsoft.Extensions.Logging;
using HuliacDev.Utils;
using ZLogger;

namespace App
{
    // 서버 연동 여부에 따라 체험자 이름을 결정한다.
    // Visitor.json은 AppSettingsProvider와 동일한 방식으로 최초 1회만 로드해 모든 소비자가 공유한다.
    public class VisitorInfoProvider : IDisposable
    {
        private const string VisitorFileName = "Visitor.json";
        private const string FallbackName = "체험자";

        private readonly ILogger<VisitorInfoProvider> _logger;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Task<VisitorData> _loadTask;
        private bool _isLoadStarted;
        private readonly object _lock = new object();

        /// <summary>
        /// 로거를 생성자 주입으로 받는다.
        /// </summary>
        public VisitorInfoProvider(ILogger<VisitorInfoProvider> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 화면에 표시할 체험자 이름을 반환한다 (서버 미연동 시 Visitor.json의 기본 이름).
        /// </summary>
        public async UniTask<string> GetNameAsync(CancellationToken cancellationToken = default)
        {
            VisitorData data = await LoadDataAsync(cancellationToken);

            if (data.isServerConnected)
            {
                // TODO: 서버 연동(예: QR 스캔)으로 실제 체험자 이름을 조회하도록 구현.
                // 서버 API가 준비되기 전까지는 기본 이름으로 대체함.
                if (_logger != null) _logger.ZLogWarning($"[VisitorInfoProvider] isServerConnected=true이지만 서버 연동이 아직 구현되지 않아 기본 이름으로 대체합니다.");
            }

            return string.IsNullOrEmpty(data.defaultUserName) ? FallbackName : data.defaultUserName;
        }

        /// <summary>
        /// Visitor.json 기준으로 서버 연동 사용 여부를 반환한다.
        /// </summary>
        public async UniTask<bool> IsServerConnectedAsync(CancellationToken cancellationToken = default)
        {
            VisitorData data = await LoadDataAsync(cancellationToken);
            return data.isServerConnected;
        }

        /// <summary>
        /// Visitor.json을 최초 1회만 로드하고 이후 호출은 같은 로드 태스크를 공유한다.
        /// </summary>
        private async UniTask<VisitorData> LoadDataAsync(CancellationToken cancellationToken)
        {
            Task<VisitorData> loadTask;

            lock (_lock)
            {
                if (!_isLoadStarted)
                {
                    _isLoadStarted = true;
                    _loadTask = JsonLoader.LoadAsync<VisitorData>(VisitorFileName, _cts.Token, _logger).AsTask();
                }

                loadTask = _loadTask;
            }

            VisitorData data = await loadTask.AsUniTask().AttachExternalCancellation(cancellationToken);
            await UniTask.SwitchToMainThread(cancellationToken);
            return data;
        }

        /// <summary>
        /// 진행 중인 로드를 취소하고 CTS를 해제한다.
        /// </summary>
        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
