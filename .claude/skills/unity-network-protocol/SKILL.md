---
name: unity-network-protocol
description: Unity networking and packet synchronization guidelines — transport selection (TCP vs UDP / RUDP), packet byte packing (StructLayout Pack=1), client-side prediction, dead reckoning (extrapolation/interpolation with Lerp), and thread-safe packet dispatching. Use when implementing multiplayer networking, UDP/TCP socket communications, hardware/sensor packet protocols (e.g. RFID, serial, OSC, custom controllers), or latency-compensating client synchronization.
---

# Unity 네트워크 프로토콜 & 동기화 가이드

이 스킬은 Unity 프로젝트에서 멀티플레이어 동기화, 커스텀 TCP/UDP 소켓 통신, 하드웨어/센서 연동(RFID, 시리얼, 컨트롤러 등) 패킷을 다룰 때 하드웨어 레벨의 지연(Latency)과 데이터 무결성을 다루기 위한 실무 가이드다.

---

## 1. 전송 프로토콜 선택 기준 (TCP vs UDP vs RUDP)

물리적 인터넷 회선이나 센서 통신 지연(수십~수백 ms)을 고려하여 데이터 특성에 맞는 프로토콜을 선택한다.

- **TCP (연결형, 신뢰성 보장)**:
  - 패킷 유실 시 커널이 무조건 재전송하여 100% 무결성을 보장하지만, 이전 패킷이 도착할 때까지 후속 패킷 전달을 대기시키는 **Head-of-Line (HOL) 블로킹**이 발생한다.
  - **적용 대상**: 로그인, 세션 셋업, 결제, 결과 저장, 씬 전환 트리거처럼 "패킷 유실 시 게임 로직이 파탄 나는 데이터".
- **UDP (비연결형, 초고속 전송)**:
  - 3-Way Handshake가 없고 오버헤드가 극도로 적으며, 패킷이 유실되어도 후속 패킷을 즉시 전달한다.
  - **적용 대상**: 초당 수십 회 갱신되는 캐릭터 좌표, 회전, 아날로그 센서 입력값처럼 "새 데이터가 이전 데이터를 덮어써도 무방한 연속 상태 데이터".
- **RUDP (Reliable UDP)**:
  - UDP 기반으로 동작하되, 중요 패킷(스킬 시전, 인터랙션 이벤트 등)에만 자체 시퀀스 번호와 ACK/재전송 로직을 적용한 형태. 상용 게임 엔진 및 실시간 액션 동기화 표준.

---

## 2. 패킷 구조체 정의와 바이트 패킹 (Byte Packing)

서버나 외부 장치(C/C++, 임베디드, 센서)와 바이너리 패킷을 교환할 때는 CPU 아키텍처의 패딩 바이트로 인해 필드 오프셋이 어긋나는 현상을 방지해야 한다.

- **원칙**: 네트워크 패킷용 `struct`는 반드시 `[StructLayout(LayoutKind.Sequential, Pack = 1)]`을 명시하여 패딩 바이트를 제거한다.
- **예시**:
  ```csharp
  using System.Runtime.InteropServices;

  // Pack = 1을 통해 1바이트 단위 압축 패킹 (불필요한 공백 제거)
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct PlayerMovePacket
  {
      public int packetId;     // 4바이트
      public float posX;       // 4바이트
      public float posY;       // 4바이트
      public float posZ;       // 4바이트
      public byte inputFlags;  // 1바이트
  } // 총 17바이트 (Pack=1이 없으면 CPU 정렬에 의해 20바이트로 패딩될 수 있음)
  ```
- **주의점**:
  - 패킷 내부에는 참조 타입(`string`, `object`, 맨 `byte[]` 등)을 직접 포함하지 않는다. 관리 배열 필드는 데이터가 아니라 **포인터**로 배치되므로, `Marshal.SizeOf`는 배열 내용 대신 포인터 크기(4/8바이트)를 세고 `Marshal.StructureToPtr`은 힙 주소를 복사한다. 수신 측은 그 자리에서 쓰레기 바이트를 읽는다.
  - 고정 길이 바이트 배열이 필요하면 인라인 마샬링을 명시한다: `[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] payload;` (또는 unsafe 컨텍스트에서 `fixed byte payload[16];`). 그 외에는 Blittable 기본 타입만 포함한다.
    - 둘 중 고를 때 기준은 GC다. `ByValArray`는 구조체를 비Blittable로 만들고 역직렬화할 때마다 관리 힙에 `byte[]`를 새로 할당하므로, 초당 수십~수백 패킷이 오가는 핫패스에서는 `fixed byte`가 유리하다(구조체 안에 데이터가 인라인으로 남아 할당이 0). 대신 `fixed`는 `unsafe` 컨텍스트가 필요하다.
  - 타 기기(Big-Endian)와 통신하는 경우 정수/실수의 엔디언(Endianness) 변환을 확인한다 (`BitConverter.IsLittleEndian`).
  - 필드 순서는 상대방과 합의한 와이어 포맷 그대로 둔다. 패딩을 줄이겠다고 바이트 크기순으로 재정렬하지 않는다(unity-stack-scaffold 14번의 정렬 규칙은 패킷에 적용되지 않는다).
- **변환은 직접 짜지 않고 `HuliacDev.Network.PacketUtility`를 쓴다.** `Marshal.AllocHGlobal`/`PtrToStructure` 보일러플레이트를 매번 재작성하면 오프셋·버퍼 길이 검증이 빠지기 쉽다.
  ```csharp
  // 수신: 바이트 -> 구조체 (버퍼 길이 부족 시 예외로 걸러짐)
  PlayerMovePacket packet = PacketUtility.FromBytes<PlayerMovePacket>(buffer);

  // 송신: 구조체 -> 기존 버퍼에 기록 (GC 무할당 오버로드)
  int written = PacketUtility.ToBytes(in packet, sendBuffer);

  // 버퍼 크기 산정
  int size = PacketUtility.GetPacketSize<PlayerMovePacket>();
  ```

---

## 3. 지연(Latency) 보상과 눈속임 기법

네트워크 왕복 시간(RTT)으로 인한 인풋랙을 숨기고 자연스러운 화면을 연출하기 위해 두 가지 핵심 기법을 적용한다.

### ① 클라이언트 사이드 예측 (Client-Side Prediction) & 조정 (Reconciliation)
- 사용자가 조작(이동, 키 입력 등)을 입력했을 때, 서버의 승인 응답을 기다리지 않고 **클라이언트에서 로컬 결과를 화면에 즉시 선반영**한다.
- 이후 도착한 서버의 공식 상태와 로컬 예측 간에 유의미한 오차가 발생했을 때만 서버 좌표로 부드럽게 조정(Reconciliation/Rollback)한다.

### ② 데드 레코닝 (Dead Reckoning, 추측 항법) & 보간 (Lerp)
- 대역폭 절약을 위해 상대 플레이어나 센서 위치 패킷은 렌더링 프레임(60fps)보다 낮은 주기(10~20Hz)로 수신된다.
- **외삽 (Dead Reckoning)**: 패킷이 오지 않는 공백 시간 동안 상대방의 `마지막 수신 위치 + (속도 벡터 * 경과 시간)`을 계산해 미래 위치를 추측한다.
- **선형 보간 (Lerp)**: 새 수신 패킷이 도착했을 때 순간이동 렉이 발생하지 않도록 `Vector3.Lerp` 또는 감속 보간을 사용하여 현재 추측 위치에서 실제 위치로 부드럽게 수렴시킨다:
  ```csharp
  // 매 프레임 수신 목표 지점으로 부드럽게 보간
  transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * _interpolationSpeed);
  ```

---

## 4. 멀티스레드 소켓 I/O와 Unity 메인 스레드 동기화

소켓 읽기(`Socket.ReceiveAsync`, `UdpClient.ReceiveAsync`)는 절대 메인 스레드를 블로킹해서는 안 되며, 수신된 데이터는 스레드 안전하게 Unity 월드로 전달되어야 한다. 백그라운드→메인 스레드 복귀의 일반 원칙(`UniTask.SwitchToMainThread`, volatile/Interlocked 공유 상태)은 unity-stack-scaffold 스킬 15번을 따른다 — 여기서는 소켓 수신 루프에 특화된 패턴만 다룬다.

- 패킷 수신 및 역직렬화는 백그라운드 워커 스레드(`UniTask.RunOnThreadPool` 또는 비동기 태스크)에서 수행하고, 역직렬화가 끝난 이벤트/데이터는 `MessagePipe`를 통해 발행하거나 `UniTask.SwitchToMainThread()`로 복귀한 뒤 게임 로직을 갱신한다:
  ```csharp
  private async UniTaskVoid StartReceiveLoopAsync(CancellationToken ct)
  {
      // 수신 버퍼는 루프 밖에서 한 번만 확보해 재사용한다 (패킷마다 new byte[] 금지).
      byte[] buffer = new byte[PacketUtility.GetPacketSize<PlayerMovePacket>()];

      try
      {
          while (!ct.IsCancellationRequested)
          {
              // 매 회차 시작에 반드시 백그라운드로 내려간다. 직전 회차에서 메인 스레드로
              // 올라온 채 그대로 돌면 블로킹 수신 구현에서 메인 스레드가 멈춘다.
              await UniTask.SwitchToThreadPool();

              int bytesRead = await ReceiveFromSocketAsync(buffer, ct);

              // TCP는 상대가 정상 종료(graceful close)하면 0바이트를 반환한다.
              // 이 가드가 없으면 연결이 끊긴 뒤 루프가 0바이트를 무한히 읽으며 CPU를 태운다.
              if (bytesRead <= 0)
              {
                  break;
              }

              PlayerMovePacket packet = PacketUtility.FromBytes<PlayerMovePacket>(buffer);

              // Unity 메인 스레드로 전환하여 씬 상태 반영
              await UniTask.SwitchToMainThread(cancellationToken: ct);
              ApplyPacketToGame(packet);
          }
      }
      catch (OperationCanceledException)
      {
          // 오브젝트 파괴로 인한 정상적인 취소
      }
  }
  ```
