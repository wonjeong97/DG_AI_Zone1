using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Cysharp.Threading.Tasks;
using Data;
using HuliacDev.App;
using MessagePipe;
using Network;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 아두이노에 보내는 명령 문자열, exit가 enter 한 번에 한 번만 나가는지, Arduino.json 주소 검사, 실제 UDP 패킷 내용,
    /// 응답(보낸 명령 미러링)이 없을 때 다시 보내기·명령 순서·앱 종료 때 남은 명령 보내기를 검증한다.
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
        /// 레벨1(태양광) success는 정답 방향 번호를 붙인다 — 동쪽 1, 서쪽 2, 남쪽 3.
        /// </summary>
        [TestCase(Constants.Directions.East, "1_success_1")]
        [TestCase(Constants.Directions.West, "1_success_2")]
        [TestCase(Constants.Directions.South, "1_success_3")]
        public void 레벨1_성공은_정답_방향_번호를_붙인다(string direction, string expected)
        {
            Assert.IsTrue(ArduinoUdpClient.TryFormatSolarSuccess(0, direction, out string command));
            Assert.AreEqual(expected, command);
        }

        /// <summary>
        /// 동쪽·서쪽·남쪽이 아닌 방향은 레벨1 success 명령을 만들지 않는다 — 아두이노에 정해진 번호가 없다.
        /// </summary>
        [TestCase(Constants.Directions.North)]
        [TestCase(null)]
        public void 번호가_없는_방향은_레벨1_성공_명령을_만들지_않는다(string direction)
        {
            Assert.IsFalse(ArduinoUdpClient.TryFormatSolarSuccess(0, direction, out string command));
            Assert.IsNull(command);
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
            using ArduinoUdpClient arduino = new ArduinoUdpClient(null, null);
            ArduinoSettings settings = new ArduinoSettings { host = host, port = port };

            Assert.AreEqual(expected, arduino.TryGetEndPoint(settings, out IPEndPoint endPoint));
            if (expected) Assert.AreEqual(new IPEndPoint(IPAddress.Parse(host), port), endPoint);
        }

        /// <summary>
        /// Arduino.json의 응답 대기 시간·보내는 횟수가 0 이하면 기본값(3초·3회)으로, 너무 긴 대기 시간은 최댓값으로 바로잡는다 —
        /// 0초면 아두이노가 답하기 전에 다시 보내 같은 명령이 여러 번 실행된다.
        /// </summary>
        [TestCase(0f, 0, Constants.Arduino.DefaultReplyTimeoutSeconds, Constants.Arduino.DefaultMaxSendAttempts, true)]
        [TestCase(-1f, -2, Constants.Arduino.DefaultReplyTimeoutSeconds, Constants.Arduino.DefaultMaxSendAttempts, true)]
        [TestCase(1e15f, 5, Constants.Arduino.MaxReplyTimeoutSeconds, 5, true)]
        [TestCase(2f, 4, 2f, 4, false)]
        public void 응답_대기_시간과_횟수는_범위_안으로_바로잡는다(float timeout, int attempts, float expectedTimeout, int expectedAttempts, bool expectedChanged)
        {
            ArduinoSettings settings = new ArduinoSettings { replyTimeoutSeconds = timeout, maxSendAttempts = attempts };

            Assert.AreEqual(expectedChanged, settings.ClampToValid());
            Assert.AreEqual(expectedTimeout, settings.replyTimeoutSeconds);
            Assert.AreEqual(expectedAttempts, settings.maxSendAttempts);
        }

        /// <summary>
        /// 명령은 줄바꿈 없는 아스키 문자열 그대로 한 패킷으로 간다 — 이 PC 안의 루프백 주소로 받아 확인한다.
        /// </summary>
        [Test]
        public void 명령은_줄바꿈_없는_아스키_한_패킷으로_간다()
        {
            using UdpClient receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            receiver.Client.ReceiveTimeout = 2000;
            using ArduinoUdpClient arduino = new ArduinoUdpClient(null, null);

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
            ArduinoUdpClient arduino = new ArduinoUdpClient(null, null);
            arduino.Dispose();

            Assert.IsFalse(arduino.SendTo("1_exit", (IPEndPoint)receiver.Client.LocalEndPoint));
            Assert.AreEqual(0, receiver.Available, "정리된 뒤에 명령이 나감");
        }

        /// <summary>
        /// 아두이노가 보낸 명령을 그대로 돌려주면 받은 것으로 끝내고, 제한 시간이 지나도 다시 보내지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 응답이_오면_다시_보내지_않는다() => UniTask.ToCoroutine(async () =>
        {
            using UdpClient fakeArduino = CreateFakeArduino();
            using LoopbackArduinoUdpClient arduino = new LoopbackArduinoUdpClient(fakeArduino, 0.5f, 3);
            UniTask<List<string>> replied = ReplyAsync(fakeArduino, 1, null);

            arduino.SendResult(0, true);

            CollectionAssert.AreEqual(new[] { "1_success" }, await replied.AwaitWithRealtimeTimeout());
            await DelayRealtime(0.8f);
            Assert.AreEqual(0, ReceiveAll(fakeArduino).Count, "응답을 받았는데 다시 보냄");
        });

        /// <summary>
        /// 레벨1 success는 방향 번호를 붙여 보내고 바로 뒤에 제자리(남쪽) success를 보내며, 번호가 없는 방향이면 아무것도 보내지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 레벨1_성공은_방향_번호를_붙여_보내고_제자리로_되돌린다() => UniTask.ToCoroutine(async () =>
        {
            using UdpClient fakeArduino = CreateFakeArduino();
            using LoopbackArduinoUdpClient arduino = new LoopbackArduinoUdpClient(fakeArduino, 0.5f, 3);
            UniTask<List<string>> replied = ReplyAsync(fakeArduino, 2, null);

            arduino.SendSolarSuccess(0, Constants.Directions.North);
            arduino.SendSolarSuccess(0, Constants.Directions.West);

            CollectionAssert.AreEqual(new[] { "1_success_2", "1_success_3" }, await replied.AwaitWithRealtimeTimeout());
            await DelayRealtime(0.8f);
            Assert.AreEqual(0, ReceiveAll(fakeArduino).Count, "번호가 없는 방향의 명령이나 재전송이 나감");
        });

        /// <summary>
        /// 정답이 제자리(남쪽)면 패널이 이미 제자리로 가므로 되돌리는 명령을 따로 보내지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 레벨1_남쪽_성공은_한_번만_보낸다() => UniTask.ToCoroutine(async () =>
        {
            using UdpClient fakeArduino = CreateFakeArduino();
            using LoopbackArduinoUdpClient arduino = new LoopbackArduinoUdpClient(fakeArduino, 0.5f, 3);
            UniTask<List<string>> replied = ReplyAsync(fakeArduino, 1, null);

            arduino.SendSolarSuccess(0, Constants.Directions.South);

            CollectionAssert.AreEqual(new[] { "1_success_3" }, await replied.AwaitWithRealtimeTimeout());
            await DelayRealtime(0.8f);
            Assert.AreEqual(0, ReceiveAll(fakeArduino).Count, "남쪽 success가 두 번 나감");
        });

        /// <summary>
        /// 응답이 없으면 제한 시간마다 같은 명령을 다시 보내고, 최대 횟수(첫 전송 포함)에서 멈춘다.
        /// </summary>
        [UnityTest]
        public IEnumerator 응답이_없으면_최대_횟수까지_다시_보낸다() => UniTask.ToCoroutine(async () =>
        {
            using UdpClient fakeArduino = CreateFakeArduino();
            using LoopbackArduinoUdpClient arduino = new LoopbackArduinoUdpClient(fakeArduino, 0.2f, 3);

            arduino.SendResult(0, false);
            await DelayRealtime(1f);

            CollectionAssert.AreEqual(new[] { "1_fail", "1_fail", "1_fail" }, ReceiveAll(fakeArduino), "최대 횟수만큼 보내지 않음");
        });

        /// <summary>
        /// 보낸 명령과 다른 문자열이 돌아오면 응답으로 치지 않고 다시 보낸다.
        /// </summary>
        [UnityTest]
        public IEnumerator 다른_문자열이_돌아오면_다시_보낸다() => UniTask.ToCoroutine(async () =>
        {
            using UdpClient fakeArduino = CreateFakeArduino();
            using LoopbackArduinoUdpClient arduino = new LoopbackArduinoUdpClient(fakeArduino, 0.3f, 2);
            UniTask<List<string>> replied = ReplyAsync(fakeArduino, 1, "2_success");

            arduino.SendResult(0, true);

            CollectionAssert.AreEqual(new[] { "1_success" }, await replied.AwaitWithRealtimeTimeout());
            await DelayRealtime(0.8f);
            CollectionAssert.AreEqual(new[] { "1_success" }, ReceiveAll(fakeArduino), "다른 문자열을 응답으로 쳐 다시 보내지 않음");
        });

        /// <summary>
        /// 응답을 기다리는 동안 뒤 명령이 들어오면 앞 명령은 더 다시 보내지 않고, 순서를 지켜 뒤 명령을 보낸다.
        /// </summary>
        [UnityTest]
        public IEnumerator 뒤_명령이_기다리면_앞_명령은_다시_보내지_않는다() => UniTask.ToCoroutine(async () =>
        {
            using UdpClient fakeArduino = CreateFakeArduino();
            using LoopbackArduinoUdpClient arduino = new LoopbackArduinoUdpClient(fakeArduino, 0.2f, 3);

            arduino.SendEnter(0);
            arduino.SendResult(0, true);
            await DelayRealtime(1.2f);

            CollectionAssert.AreEqual(new[] { "1_enter", "1_success", "1_success", "1_success" }, ReceiveAll(fakeArduino));
        });

        /// <summary>
        /// 비활동 타임아웃과 결과 화면 다음 버튼이 겹쳐도 exit는 enter 뒤에 한 번만 나간다.
        /// </summary>
        [UnityTest]
        public IEnumerator 비활동_타임아웃과_다음_버튼이_겹쳐도_exit는_한_번() => UniTask.ToCoroutine(async () =>
        {
            using UdpClient fakeArduino = CreateFakeArduino();
            FakeTimeoutSubscriber timeout = new FakeTimeoutSubscriber();
            using LoopbackArduinoUdpClient arduino = new LoopbackArduinoUdpClient(fakeArduino, 0.5f, 3, timeout);
            UniTask<List<string>> replied = ReplyAsync(fakeArduino, 2, null);

            arduino.SendEnter(2);
            timeout.Publish();
            timeout.Publish();
            arduino.SendExit();

            CollectionAssert.AreEqual(new[] { "3_enter", "3_exit" }, await replied.AwaitWithRealtimeTimeout());
            await DelayRealtime(0.8f);
            Assert.AreEqual(0, ReceiveAll(fakeArduino).Count, "exit가 두 번 나감");
        });

        /// <summary>
        /// 앱이 끝나면 응답을 기다리지 않고 차례를 기다리던 명령과 남은 exit를 한 번씩 보낸 뒤 더 보내지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 앱이_끝나면_남은_명령과_exit를_한_번씩_보낸다() => UniTask.ToCoroutine(async () =>
        {
            using UdpClient fakeArduino = CreateFakeArduino();
            LoopbackArduinoUdpClient arduino = new LoopbackArduinoUdpClient(fakeArduino, 1f, 3);

            arduino.SendEnter(0);
            arduino.SendResult(0, false);
            arduino.Dispose();
            await DelayRealtime(1.5f);

            CollectionAssert.AreEqual(new[] { "1_enter", "1_fail", "1_exit" }, ReceiveAll(fakeArduino));
        });

        /// <summary>
        /// 루프백 주소의 빈 포트에서 명령을 받는 가짜 아두이노를 연다.
        /// </summary>
        private static UdpClient CreateFakeArduino()
        {
            UdpClient fakeArduino = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            fakeArduino.Client.ReceiveTimeout = 2000;
            return fakeArduino;
        }

        /// <summary>
        /// 실제 시간으로 기다린다.
        /// </summary>
        private static UniTask DelayRealtime(float seconds) => UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime);

        /// <summary>
        /// 가짜 아두이노가 명령 count개를 받아 하나씩 replyText(null이면 받은 명령 그대로)를 보낸 쪽으로 돌려주고, 받은 명령을 돌려준다.
        /// </summary>
        private static async UniTask<List<string>> ReplyAsync(UdpClient fakeArduino, int count, string replyText)
        {
            List<string> received = new List<string>();
            for (int i = 0; i < count; i++)
            {
                UdpReceiveResult packet = await fakeArduino.ReceiveAsync();
                received.Add(Encoding.ASCII.GetString(packet.Buffer));

                byte[] reply = replyText == null ? packet.Buffer : Encoding.ASCII.GetBytes(replyText);
                await fakeArduino.SendAsync(reply, reply.Length, packet.RemoteEndPoint);
            }

            return received;
        }

        /// <summary>
        /// 가짜 아두이노에 쌓인 명령을 모두 읽어 받은 순서대로 돌려준다.
        /// </summary>
        private static List<string> ReceiveAll(UdpClient fakeArduino)
        {
            List<string> received = new List<string>();
            IPEndPoint sender = null;
            while (fakeArduino.Available > 0)
                received.Add(Encoding.ASCII.GetString(fakeArduino.Receive(ref sender)));

            return received;
        }

        /// <summary>
        /// Arduino.json 대신 가짜 아두이노(루프백) 주소와 테스트용 대기 시간·횟수로 보내는 클라이언트.
        /// </summary>
        private sealed class LoopbackArduinoUdpClient : ArduinoUdpClient
        {
            private readonly ArduinoSettings _settings;

            /// <summary>
            /// 가짜 아두이노의 주소로 설정을 만든다.
            /// </summary>
            public LoopbackArduinoUdpClient(UdpClient fakeArduino, float replyTimeoutSeconds, int maxSendAttempts,
                ISubscriber<InactivityTimeoutEvent> inactivityTimeoutSubscriber = null) : base(null, inactivityTimeoutSubscriber)
            {
                IPEndPoint endPoint = (IPEndPoint)fakeArduino.Client.LocalEndPoint;
                _settings = new ArduinoSettings
                {
                    host = endPoint.Address.ToString(),
                    port = endPoint.Port,
                    replyTimeoutSeconds = replyTimeoutSeconds,
                    maxSendAttempts = maxSendAttempts,
                };
            }

            /// <summary>
            /// 파일 대신 만들어 둔 설정을 돌려준다.
            /// </summary>
            protected override UniTask<ArduinoSettings> LoadSettingsAsync() => UniTask.FromResult(_settings);
        }

        /// <summary>
        /// 비활동 타이머 대신 테스트가 직접 타임아웃 이벤트를 낸다.
        /// </summary>
        private sealed class FakeTimeoutSubscriber : ISubscriber<InactivityTimeoutEvent>, IDisposable
        {
            private IMessageHandler<InactivityTimeoutEvent> _handler;

            /// <summary>
            /// 구독한 처리기를 기억한다.
            /// </summary>
            public IDisposable Subscribe(IMessageHandler<InactivityTimeoutEvent> handler, params MessageHandlerFilter<InactivityTimeoutEvent>[] filters)
            {
                _handler = handler;
                return this;
            }

            /// <summary>
            /// 비활동 타임아웃 이벤트를 낸다.
            /// </summary>
            public void Publish() => _handler?.Handle(new InactivityTimeoutEvent());

            /// <summary>
            /// 구독을 끊는다.
            /// </summary>
            public void Dispose() => _handler = null;
        }
    }
}
