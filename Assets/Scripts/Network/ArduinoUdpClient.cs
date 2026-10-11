using System;
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
    // 아두이노는 받은 명령을 보낸 쪽 포트로 그대로 돌려준다(1_exit를 받으면 1_exit) — 게임 진행은 응답을 기다리지 않고 로그로만 남긴다.
    /// <summary>
    /// 아두이노에 레벨 진입·결과·종료 명령을 UDP로 보내고 응답을 로그로 남긴다.
    /// </summary>
    public class ArduinoUdpClient : IDisposable
    {
        private readonly ILogger<ArduinoUdpClient> _logger;
        private readonly ArduinoEnterState _enterState = new ArduinoEnterState();

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
        /// Arduino.json에서 주소를 읽어 명령을 보낸다 — 호출부는 기다리지 않는다.
        /// </summary>
        private void Send(string command)
        {
            SendAsync(command).Forget();
        }

        /// <summary>
        /// Arduino.json을 읽은 뒤 명령을 한 패킷으로 보낸다.
        /// </summary>
        private async UniTaskVoid SendAsync(string command)
        {
            // 씬 수명과 묶지 않는다 — 다음 버튼의 exit처럼 씬을 떠나는 순간 보내는 명령도 끝까지 나가야 한다
            ArduinoSettings settings = await JsonLoader.LoadAsync<ArduinoSettings>(Constants.SettingsFiles.Arduino, CancellationToken.None, _logger);
            if (TryGetEndPoint(settings, out IPEndPoint endPoint))
                SendTo(command, endPoint);
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
            // UDP라 아두이노가 꺼져 있어도 보내기는 성공한다 — 받았는지는 아두이노 응답 로그로만 확인할 수 있다
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
        /// 소켓이 닫힐 때까지 아두이노 응답을 받아 로그로 남긴다.
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

                    if (_logger != null) _logger.ZLogWarning($"[ArduinoUdpClient] 아두이노 응답을 받지 못해 응답 받기를 멈춥니다(명령은 계속 보냄): {e.Message}");
                    return;
                }

                string reply = Encoding.ASCII.GetString(result.Buffer).Trim();
                if (_logger != null) _logger.ZLogInformation($"[ArduinoUdpClient] 아두이노({result.RemoteEndPoint}) 응답: '{reply}'");
            }
        }

        /// <summary>
        /// 소켓을 닫아 응답 받기를 끝낸다 — 앱이 끝나 컨테이너가 정리될 때 불린다.
        /// </summary>
        public void Dispose()
        {
            _isDisposed = true;

            UdpClient client = _client;
            _client = null;
            client?.Dispose();
        }
    }
}
