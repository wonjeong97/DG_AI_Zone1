using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Data;
using Microsoft.Extensions.Logging;
using Wonjeong.Utils;
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

        public VisitorInfoProvider(ILogger<VisitorInfoProvider> logger)
        {
            _logger = logger;
        }

        public async UniTask<string> GetNameAsync(CancellationToken cancellationToken = default)
        {
            VisitorData data = await LoadDataAsync(cancellationToken);

            if (data.isServerConnected)
            {
                // TODO: 서버 연동(예: QR 스캔)으로 실제 체험자 이름을 조회하도록 구현.
                // 서버 API가 준비되기 전까지는 기본 이름으로 대체함.
                _logger?.ZLogWarning($"[VisitorInfoProvider] isServerConnected=true이지만 서버 연동이 아직 구현되지 않아 기본 이름으로 대체합니다.");
            }

            return string.IsNullOrEmpty(data.defaultUserName) ? FallbackName : data.defaultUserName;
        }

        public async UniTask<bool> IsServerConnectedAsync(CancellationToken cancellationToken = default)
        {
            VisitorData data = await LoadDataAsync(cancellationToken);
            return data.isServerConnected;
        }

        private async UniTask<VisitorData> LoadDataAsync(CancellationToken cancellationToken)
        {
            Task<VisitorData> loadTask;

            lock (_lock)
            {
                if (!_isLoadStarted)
                {
                    _isLoadStarted = true;
                    _loadTask = JsonLoader.LoadAsync<VisitorData>(VisitorFileName, _cts.Token).AsTask();
                }

                loadTask = _loadTask;
            }

            VisitorData data = await loadTask.AsUniTask().AttachExternalCancellation(cancellationToken);
            await UniTask.SwitchToMainThread(cancellationToken);
            return data;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
