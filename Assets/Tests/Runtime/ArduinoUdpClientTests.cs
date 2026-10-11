using System.Net;
using System.Net.Sockets;
using System.Text;
using Data;
using Network;
using NUnit.Framework;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 아두이노에 보내는 명령 문자열, exit가 enter 한 번에 한 번만 나가는지, Arduino.json 주소 검사, 실제 UDP 패킷 내용을 검증한다.
    /// </summary>
    public class ArduinoUdpClientTests
    {
        /// <summary>
        /// 명령은 레벨 번호(1부터)를 앞에 붙인 문자열이다 — 아두이노와 맞춘 형식이다.
        /// </summary>
        [TestCase(Constants.Arduino.EnterFormat, 0, "1_enter")]
        [TestCase(Constants.Arduino.SuccessFormat, 2, "3_success")]
        [TestCase(Constants.Arduino.FailFormat, 1, "2_fail")]
        [TestCase(Constants.Arduino.ExitFormat, 4, "5_exit")]
        public void 명령은_레벨_번호를_붙인_문자열이다(string format, int levelIndex, string expected)
        {
            Assert.AreEqual(expected, ArduinoUdpClient.FormatCommand(format, levelIndex));
        }

        /// <summary>
        /// enter 뒤 exit는 한 번만 나간다 — 다음 버튼 연타나 다음 버튼 뒤 비활동 타임아웃이 exit를 다시 보내지 않는다.
        /// </summary>
        [Test]
        public void exit는_enter_뒤_한_번만_나간다()
        {
            ArduinoEnterState state = new ArduinoEnterState();
            state.MarkEntered(2);

            Assert.IsTrue(state.TryTakeExit(out int levelIndex));
            Assert.AreEqual(2, levelIndex);
            Assert.IsFalse(state.TryTakeExit(out _), "두 번째 exit가 나감");
        }

        /// <summary>
        /// enter를 보낸 적이 없으면 exit를 보내지 않는다 — 인트로에서 난 비활동 타임아웃 등.
        /// </summary>
        [Test]
        public void enter가_없으면_exit를_보내지_않는다()
        {
            Assert.IsFalse(new ArduinoEnterState().TryTakeExit(out _));
        }

        /// <summary>
        /// exit 없이 다른 레벨을 다시 고르면 마지막에 고른 레벨의 exit가 나가고, 다음 enter 뒤에는 다시 exit가 나간다.
        /// </summary>
        [Test]
        public void 마지막에_고른_레벨의_exit가_나간다()
        {
            ArduinoEnterState state = new ArduinoEnterState();
            state.MarkEntered(0);
            state.MarkEntered(3);

            Assert.IsTrue(state.TryTakeExit(out int levelIndex));
            Assert.AreEqual(3, levelIndex);

            state.MarkEntered(1);
            Assert.IsTrue(state.TryTakeExit(out levelIndex), "exit를 한 번 보낸 뒤 다음 enter의 exit가 나가지 않음");
            Assert.AreEqual(1, levelIndex);
        }

        /// <summary>
        /// Arduino.json의 host는 IP 주소만, port는 1~65535만 받는다.
        /// </summary>
        [TestCase("192.168.0.13", 9000, true)]
        [TestCase("", 9000, false)]
        [TestCase("arduino.local", 9000, false)]
        [TestCase("192.168.0.13", 0, false)]
        [TestCase("192.168.0.13", 65536, false)]
        public void 주소는_IP와_범위_안의_포트만_받는다(string host, int port, bool expected)
        {
            using ArduinoUdpClient arduino = new ArduinoUdpClient(null);
            ArduinoSettings settings = new ArduinoSettings { host = host, port = port };

            Assert.AreEqual(expected, arduino.TryGetEndPoint(settings, out IPEndPoint endPoint));
            if (expected) Assert.AreEqual(new IPEndPoint(IPAddress.Parse(host), port), endPoint);
        }

        /// <summary>
        /// 명령은 줄바꿈 없는 아스키 문자열 그대로 한 패킷으로 간다 — 이 PC 안의 루프백 주소로 받아 확인한다.
        /// </summary>
        [Test]
        public void 명령은_줄바꿈_없는_아스키_한_패킷으로_간다()
        {
            using UdpClient receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            receiver.Client.ReceiveTimeout = 2000;
            using ArduinoUdpClient arduino = new ArduinoUdpClient(null);

            Assert.IsTrue(arduino.SendTo("1_enter", (IPEndPoint)receiver.Client.LocalEndPoint));

            IPEndPoint sender = null;
            byte[] received = receiver.Receive(ref sender);
            Assert.AreEqual("1_enter", Encoding.ASCII.GetString(received));
        }

        /// <summary>
        /// 앱이 끝나 정리된 뒤 늦게 도착한 명령은 소켓을 다시 열지 않고 버린다.
        /// </summary>
        [Test]
        public void 정리된_뒤에는_명령을_보내지_않는다()
        {
            using UdpClient receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            ArduinoUdpClient arduino = new ArduinoUdpClient(null);
            arduino.Dispose();

            Assert.IsFalse(arduino.SendTo("1_exit", (IPEndPoint)receiver.Client.LocalEndPoint));
            Assert.AreEqual(0, receiver.Available, "정리된 뒤에 명령이 나감");
        }
    }
}
