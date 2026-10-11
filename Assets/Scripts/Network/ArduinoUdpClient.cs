using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Data;
using HuliacDev.Utils;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Network
{
    // 조명·장치를 움직이는 아두이노(1존 공용 1대)에 명령 문자열을 UDP로 보낸다. 좌·우 PC가 같은 명령을 보내면 아두이노가 쌓아 차례로 실행한다.
    // 아두이노 주소는 StreamingAssets/Json/Arduino.json에서 보낼 때마다 읽어 현장에서 파일만 고쳐도 다음 명령부터 반영된다.
    // 아두이노는 받은 명령을 보낸 쪽 포트로 그대로 돌려준다(1_exit를 받으면 1_exit). 정해진 시간 안에 돌아오지 않으면 같은 명령을 다시 보낸다.
    // 게임 진행은 응답을 기다리지 않는다 — 명령은 이 클래스 안에서 차례로 보내고 응답은 로그로 남긴다.
    /// <summary>
    /// 아두이노에 레벨 진입·결과·종료 명령을 UDP로 보내고 응답이 없으면 다시 보낸다.
    /// </summary>
    public class ArduinoUdpClient : IDisposable
    {
        // Arduino.json의 응답 대기 시간이 0 이하로 잘못 적혀도 응답을 받을 틈은 남긴다
        private const float MinReplyTimeoutSeconds = 0.1f;

        private readonly ILogger<ArduinoUdpClient> _logger;
        private readonly ArduinoEnterState _enterState = new ArduinoEnterState();

        // 명령은 하나씩 응답을 확인하며 보낸다 — 앞 명령을 다시 보내는 동안 뒤 명령이 먼저 나가 순서가 바뀌지 않게(success보다 exit가 먼저 등)
        private readonly Queue<string> _sendQueue = new Queue<string>();
        private bool _isSending;

        // 지금 응답을 기다리는 명령과, 같은 문자열이 돌아오면 완료할 소스
        private string _awaitedReply;
        private UniTaskCompletionSource _replySource;

        // 첫 명령 때 열어 앱이 끝날 때까지 쓴다 — 아두이노가 이 소켓의 포트로 응답한다
        private UdpClient _client;
        private bool _isDisposed;

        /// <summary>
        /// 로거를 생성자 주입으로 받는다.
        /// </summary>
        public ArduinoUdpClient(ILogger<ArduinoUdpClient> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 레벨을 골라 스토리 화면이 나올 때 enter를 보내고 그 레벨을 exit 대상으로 기억한다.
        /// </summary>
        public void SendEnter(int levelIndex)
        {
            _enterState.MarkEntered(levelIndex);
            Send(FormatCommand(Constants.Arduino.EnterFormat, levelIndex));
        }

        /// <summary>
        /// 결과 화면에 들어올 때 전력 수급 상태가 양호면 success, 보통·부족이면 fail을 보낸다.
        /// </summary>
        public void SendResult(int levelIndex, bool isGood)
        {
            Send(FormatCommand(isGood ? Constants.Arduino.SuccessFormat : Constants.Arduino.FailFormat, levelIndex));
        }

        /// <summary>
        /// enter를 보낸 레벨이 있으면 그 레벨의 exit를 한 번 보낸다.
        /// </summary>
        public void SendExit()
        {
            // enter 없이 불리면(인트로·아웃트로의 비활동 타임아웃 등) 보낼 것이 없다
            if (_enterState.TryTakeExit(out int levelIndex))
                Send(FormatCommand(Constants.Arduino.ExitFormat, levelIndex));
        }

        /// <summary>
        /// 레벨 순번(0부터)을 레벨 번호(1부터)로 바꿔 명령 형식에 넣는다 — 레벨1의 enter는 1_enter.
        /// </summary>
        public static string FormatCommand(string format, int levelIndex) => ZString.Format(format, levelIndex + 1);

        /// <summary>
        /// 명령을 보낼 차례에 넣고, 보내는 중이 아니면 차례대로 보내기 시작한다 — 호출부는 기다리지 않는다.
        /// </summary>
        private void Send(string command)
        {
            if (_isDisposed) return;

            _sendQueue.Enqueue(command);
            if (!_isSending) ProcessQueueAsync().Forget();
        }

        /// <summary>
        /// 차례에 쌓인 명령을 하나씩 응답을 확인하며 보낸다.
        /// </summary>
        private async UniTaskVoid ProcessQueueAsync()
        {
            // 씬 수명과 묶지 않는다 — 다음 버튼의 exit처럼 씬을 떠나는 순간 보내는 명령도 끝까지 나가야 한다
            _isSending = true;
            try
            {
                while (_sendQueue.Count > 0 && !_isDisposed)
                    await SendWithSettingsAsync(_sendQueue.Dequeue());
            }
            catch (Exception e)
            {
                // 남은 명령은 다음 명령이 들어올 때 이어서 보낸다
                if (_logger != null) _logger.ZLogError($"[ArduinoUdpClient] 아두이노 명령을 보내다가 예외가 나 보내기를 멈춥니다: {e.Message}");
            }
            finally
            {
                _isSending = false;
            }
        }

        /// <summary>
        /// Arduino.json을 읽어 명령을 보내고 응답이 없으면 정해진 횟수까지 다시 보낸다.
        /// </summary>
        private async UniTask SendWithSettingsAsync(string command)
        {
            ArduinoSettings settings = await JsonLoader.LoadAsync<ArduinoSettings>(Constants.SettingsFiles.Arduino, CancellationToken.None, _logger);
            if (!TryGetEndPoint(settings, out IPEndPoint endPoint)) return;

            await SendAndWaitReplyAsync(command, endPoint, settings.replyTimeoutSeconds, settings.maxSendAttempts);
        }

        /// <summary>
        /// 명령을 보내고 같은 문자열의 응답을 기다린다 — 제한 시간 안에 오지 않으면 최대 횟수까지 다시 보내고, 응답을 받았는지 돌려준다.
        /// </summary>
        public async UniTask<bool> SendAndWaitReplyAsync(string command, IPEndPoint endPoint, float replyTimeoutSeconds, int maxSendAttempts)
        {
            // 응답 대기는 timeScale·프레임과 무관하게 실제 시간으로 잰다
            TimeSpan timeout = TimeSpan.FromSeconds(Math.Max(MinReplyTimeoutSeconds, replyTimeoutSeconds));
            maxSendAttempts = Math.Max(1, maxSendAttempts);

            try
            {
                for (int attempt = 1; attempt <= maxSendAttempts; attempt++)
                {
                    if (_isDisposed) return false;

                    // 앞 시도의 응답이 늦게 와도 같은 명령이므로 받은 것으로 친다
                    UniTaskCompletionSource replySource = new UniTaskCompletionSource();
                    _replySource = replySource;
                    _awaitedReply = command;

                    // 보내기에 실패해도(랜선이 잠깐 빠진 경우 등) 같은 간격으로 다시 보낸다
                    SendTo(command, endPoint);

                    bool isTimeout = await replySource.Task.TimeoutWithoutException(timeout, DelayType.Realtime);
                    if (!isTimeout) return true;
                    if (_isDisposed) return false;

                    if (attempt < maxSendAttempts && _logger != null)
                        _logger.ZLogWarning($"[ArduinoUdpClient] {timeout.TotalSeconds}초 안에 아두이노 응답이 없어 '{command}'를 다시 보냅니다 ({attempt + 1}/{maxSendAttempts}).");
                }
            }
            finally
            {
                // 포기한 명령의 응답이 늦게 와도 다음 명령의 대기를 끝내지 않게 비운다
                _awaitedReply = null;
                _replySource = null;
            }

            if (_logger != null) _logger.ZLogError($"[ArduinoUdpClient] '{command}'를 {maxSendAttempts}번 보냈지만 아두이노 응답이 없습니다 — 아두이노 전원·랜선과 Arduino.json 주소를 확인하세요.");
            return false;
        }

        /// <summary>
        /// Arduino.json의 주소를 UDP 목적지로 바꾼다 — IP 주소가 아니거나 포트가 범위 밖이면 에러를 남기고 false를 돌려준다.
        /// </summary>
        public bool TryGetEndPoint(ArduinoSettings settings, out IPEndPoint endPoint)
        {
            endPoint = null;

            // 호스트 이름은 이름 풀이를 기다리게 되므로 IP 주소만 받는다
            if (settings == null || !IPAddress.TryParse(settings.host, out IPAddress address))
            {
                if (_logger != null) _logger.ZLogError($"[ArduinoUdpClient] Arduino.json의 host '{settings?.host}'가 IP 주소가 아니어서 아두이노에 명령을 보내지 않습니다.");
                return false;
            }

            if (settings.port <= IPEndPoint.MinPort || settings.port > IPEndPoint.MaxPort)
            {
                if (_logger != null) _logger.ZLogError($"[ArduinoUdpClient] Arduino.json의 port {settings.port}가 1~{IPEndPoint.MaxPort} 밖이어서 아두이노에 명령을 보내지 않습니다.");
                return false;
            }

            endPoint = new IPEndPoint(address, settings.port);
            return true;
        }

        /// <summary>
        /// 명령을 줄바꿈 없는 아스키 문자열 그대로 한 패킷으로 보낸다.
        /// </summary>
        public bool SendTo(string command, IPEndPoint endPoint)
        {
            // UDP라 아두이노가 꺼져 있어도 보내기는 성공한다 — 받았는지는 아두이노가 돌려준 응답으로만 알 수 있다
            try
            {
                UdpClient client = GetOrOpenClient();
                if (client == null) return false;

                byte[] bytes = Encoding.ASCII.GetBytes(command);
                client.Send(bytes, bytes.Length, endPoint);

                if (_logger != null) _logger.ZLogInformation($"[ArduinoUdpClient] 아두이노({endPoint})에 '{command}'를 보냈습니다.");
                return true;
            }
            catch (Exception e)
            {
                if (_logger != null) _logger.ZLogWarning($"[ArduinoUdpClient] 아두이노({endPoint})에 '{command}'를 보내지 못했습니다: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 명령·응답에 쓸 소켓을 처음 한 번 열고 응답 받기를 시작한다.
        /// </summary>
        private UdpClient GetOrOpenClient()
        {
            // 앱이 끝나며 정리된 뒤에 늦게 도착한 명령은 소켓을 다시 열지 않는다
            if (_isDisposed) return null;
            if (_client != null) return _client;

            // 빈 포트를 하나 받아 앱이 끝날 때까지 쓴다 — 포트는 실행마다 바뀌므로 아두이노는 받은 패킷의 포트로 응답해야 한다
            _client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            ReceiveRepliesAsync(_client).Forget();
            return _client;
        }

        /// <summary>
        /// 소켓이 닫힐 때까지 아두이노 응답을 받아 로그로 남기고, 기다리던 명령의 응답이면 대기를 끝낸다.
        /// </summary>
        private async UniTaskVoid ReceiveRepliesAsync(UdpClient client)
        {
            while (client == _client)
            {
                UdpReceiveResult result;
                try
                {
                    result = await client.ReceiveAsync();
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // Windows는 보낸 명령이 ICMP로 거부되면(아두이노 프로그램이 멈춤 등) 다음 받기에서 이 예외를 낸다 — 소켓은 계속 쓸 수 있다
                    continue;
                }
                catch (Exception e)
                {
                    // Dispose로 소켓을 닫으면 기다리던 받기가 예외로 끝난다 — 정상 종료
                    if (client != _client) return;

                    // 응답을 못 받는 소켓을 그대로 두면 이후 모든 명령이 응답 없음으로 여러 번 나간다 — 닫고 다음 명령 때 새로 연다
                    if (_logger != null) _logger.ZLogWarning($"[ArduinoUdpClient] 아두이노 응답을 받다가 예외가 나 소켓을 닫고 다음 명령 때 다시 엽니다: {e.Message}");
                    _client = null;
                    client.Dispose();
                    return;
                }

                string reply = Encoding.ASCII.GetString(result.Buffer).Trim();
                if (_logger != null) _logger.ZLogInformation($"[ArduinoUdpClient] 아두이노({result.RemoteEndPoint}) 응답: '{reply}'");
                CompleteAwaitedReply(reply);
            }
        }

        /// <summary>
        /// 응답이 기다리던 명령과 같으면 대기를 끝낸다 — 다른 문자열(포기한 명령의 늦은 응답 등)은 로그만 남는다.
        /// </summary>
        private void CompleteAwaitedReply(string reply)
        {
            if (_replySource == null || reply != _awaitedReply) return;

            _replySource.TrySetResult();
        }

        /// <summary>
        /// 남은 명령을 버리고 소켓을 닫아 응답 받기를 끝낸다 — 앱이 끝나 컨테이너가 정리될 때 불린다.
        /// </summary>
        public void Dispose()
        {
            _isDisposed = true;
            _sendQueue.Clear();

            // 응답을 기다리던 명령은 대기를 바로 끝낸다(다시 보내지 않고 그대로 끝남)
            _replySource?.TrySetCanceled();

            UdpClient client = _client;
            _client = null;
            client?.Dispose();
        }
    }
}
