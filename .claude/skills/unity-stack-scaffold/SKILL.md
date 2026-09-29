---
name: unity-stack-scaffold
description: Scaffold or refactor Unity C# classes (managers, systems, services) using this project's established stack — VContainer for DI, UniTask for async, MessagePipe for pub/sub events, R3 for reactive state, ZLogger.Unity for logging, ZString for string building, DOTween for tweening — plus this project's general C#/Unity performance and architecture conventions: struct memory layout/padding, cache-locality-friendly collections, hot-path virtual-call cost, Zero-GC/boxing/LINQ avoidance, UI Canvas/Raycast/overdraw optimization, float epsilon comparisons, shader branch divergence, FSM/Command pattern usage, and ProfilerMarker instrumentation of hot or frame-blocking sections. Use this whenever the user asks to create a new manager/system/service class, wants to "이 스택으로" build or refactor something, mentions VContainer/UniTask/MessagePipe/R3/ZLogger/ZString/DOTween by name, asks about GC spikes/struct padding/cache misses/UI overdraw/float precision/shader branching in a Unity C# context, or asks to convert a coroutine/event/singleton pattern to the project's DI+async style — even if they just say "매니저 하나 만들어줘" without naming the libraries. The concrete conventions here come from this project's own Packages/com.huliacdev.template code (RootLifetimeScope, GameManagerBase, SoundManager), not generic library docs, so prefer this skill over general Unity/C# knowledge for this stack.
---

# Unity 스택 스캐폴딩 (VContainer / UniTask / MessagePipe / R3 / ZLogger / ZString)

이 스킬은 라이브러리 사용법을 처음부터 설계하는 게 아니라, 이 프로젝트의 `Packages/com.huliacdev.template` (RootLifetimeScope.cs, GameManagerBase.cs, SoundManager.cs)에 이미 정착된 실제 패턴을 재현하기 위한 것이다. 왜 이 방식인지 궁금하면 해당 파일들을 직접 열어 대조해도 된다.

## 0. 공통 C# 규칙

이 스킬은 "어떤 라이브러리를 어떤 순서/형태로 조합하는가"를 주로 다루지만, 아래 C# 작성 규칙은 이 스킬이 적용되는 모든 코드에 항상 함께 적용한다.

- **`var` 금지**: 지역 변수도 `Task<Settings> loadTask = ...`처럼 타입을 명시한다. 템플릿 코드 전체가 이 규칙을 지키고 있으므로 `var`를 쓴 코드는 리뷰에서 되돌아온다.
- **`GetComponent` 대신 `TryGetComponent`**: 실패 시 null이 조용히 전파되어 나중에 엉뚱한 곳에서 NRE가 나는 대신, bool 분기로 그 자리에서 처리한다.
- **`GetComponentInChildren` / `GetComponentInParent` 금지**: 계층 구조를 순회하며 처음 걸리는 컴포넌트를 반환하므로, 프리팹 구조가 바뀌면 조용히 다른 객체를 잡거나 null을 돌려준다(비활성 오브젝트는 기본적으로 건너뜀). `TryGetComponent` 같은 bool 버전도 없어 위 규칙과 충돌한다. 대신 `[SerializeField]`로 인스펙터에서 참조를 연결하거나 VContainer 주입을 쓴다.
  - 예외: 참조를 보관하지 않고 **존재 여부만 확인하는 DI 등록 코드**(`RootLifetimeScope.RegisterIfPresentInScene`처럼 씬 루트 아래 컴포넌트가 있는지 보고 등록 여부를 정하는 경우)는 허용한다. 등록 전이라 주입으로 대체할 수 없고, 결과를 필드에 들고 있지 않으므로 계층 변경이 조용한 오참조로 이어지지 않는다.
- **씬 탐색 API 금지**: `GameObject.Find`, `FindObjectOfType` / `FindFirstObjectByType` / `FindAnyObjectByType`, `Camera.main`을 쓰지 않는다. 위 규칙과 같은 이유로, 코드만 봐서는 무엇에 의존하는지 드러나지 않고 이름·태그·계층이 바뀌면 조용히 다른 객체를 잡거나 null을 돌려준다. 대안도 같다 — `[SerializeField]` 연결 또는 VContainer 주입.
  - `Transform.Find`(이름 기반 자식 탐색)도 같은 이유로 기본적으로 쓰지 않는다. 자식 이름은 인스펙터에서 언제든 바뀔 수 있고, 바뀌면 컴파일 오류 없이 null을 돌려준다.
  - 예외: 코드가 직접 만들고 이름을 상수로 관리하는 자식을 다시 찾는 경우는 허용한다. `UIManager.SetButton`이 `ButtonTextChildName` 상수로 자기가 만든 `"Text"` 자식을 찾는 것이 그 예다. 이름을 정하는 쪽과 찾는 쪽이 같은 상수를 공유하므로 이름이 어긋날 수 없다.
  - 이름을 모르는 기존 자식은 `foreach (Transform child in parent)`로 직계 자식을 순회하며 `TryGetComponent`로 찾는다. `UIManager`가 Unity 메뉴로 만든 `"Text (Legacy)"`·`"Text (TMP)"` 버튼의 텍스트를 재사용하는 방식이 이것이다.
- **`UnityEngine.Object` 파생 타입의 null 검사는 암시적 bool**: `MonoBehaviour`뿐 아니라 `GameObject`, `ScriptableObject`, `Material`, `Texture` 등 `UnityEngine.Object`를 상속하는 모든 타입에 적용한다. `if (reporter)` / `if (!inspectorContainer)` 형태를 쓰고, `?.`, `??`, `??=`, `is null`, `is not null`은 쓰지 않는다. 이 문법들은 Unity가 재정의한 `==`를 거치지 않아 이미 파괴된 오브젝트를 null이 아니라고 판단한다. 단 `ILogger`, `CancellationTokenSource`, DOTween `Tween` 같은 순수 C# 객체는 `if (_logger != null)`처럼 명시적 비교를 쓰며, `_fadeCts?.Dispose()` 같은 단축 연산자도 허용한다.

## 1. HuliacDev 템플릿 우선 재사용

새 기능을 처음부터 작성하기 전에, `com.huliacdev.template` 패키지(`HuliacDev.App` / `HuliacDev.Core` / `HuliacDev.UI` / `HuliacDev.Utils` / `HuliacDev.Data` / `HuliacDev.Hardware` / `HuliacDev.Network` 네임스페이스)에 이미 그 역할을 하는 베이스 클래스나 유틸리티가 있는지 먼저 확인한다. 없는 걸 새로 만드는 것보다 있는 걸 최대한 활용하는 게 우선이다.

- 씬/전역 매니저는 `MonoBehaviour`를 직접 상속하지 않고 `HuliacDev.Core.GameManagerBase`를 상속한다 (`GameManagerBase`는 제네릭이 아닌 일반 추상 클래스임). `SingletonGuard<GameManagerBase>`를 통한 싱글톤 보존, InputAction(디버그/인스펙터/마우스 토글) 연결, Reporter/RuntimeInspector 디버그 UI 연동, `AppSettingsProvider`를 통한 Settings 비동기 로드 및 `OnSettingsLoaded(Settings)` 가상 메서드 초기화가 이미 구현되어 있으므로 중복 구현하지 않는다.
- 사운드/UI/페이드/비디오 관련 요청이면 새로 만들기 전에 `HuliacDev.UI.SoundManager` / `UIManager` / `FadeManager` / `VideoManager`가 이미 그 역할을 하는지 먼저 확인하고, 있으면 확장하거나 그대로 호출한다.
- 설정/데이터 파일 로딩은 직접 파일 I/O나 JSON 파싱을 짜지 않고 `HuliacDev.Utils.JsonLoader.Load<T>` / `LoadAsync<T>` (`where T : new()`, `.json` 확장자 자동 처리, 실패 시 `new T()` 반환) 또는 VContainer에 등록된 `AppSettingsProvider`를 재사용한다.
- 전역 DI/로깅/MessagePipe 브로커 등록이 필요하면 완전히 새로운 LifetimeScope를 만들기보다 `HuliacDev.App.RootLifetimeScope`를 상속해 필요한 구성 메서드(`ConfigureLogging`, `ConfigureLogRetention`, `ConfigureMessagePipe`, `ConfigureSettings`, `ConfigureNetwork`, `ConfigureCoreComponents`, `ConfigureOptionalComponents`)만 override하는 걸 우선 고려한다.
- 상태 머신(FSM)은 `HuliacDev.Core`의 `IState<TContext>` / `StateMachine<TContext>`(및 컨텍스트 없는 `IState` / `StateMachine`)를 쓰고 새로 선언하지 않는다(22번).
- 바이너리 패킷 ↔ 구조체 변환은 `HuliacDev.Network.PacketUtility`(`FromBytes<T>`, `ToBytes<T>`, GC 무할당 `ToBytes<T>(in T, byte[], offset)`, `GetPacketSize<T>`)를 쓰고 `Marshal` 코드를 직접 짜지 않는다(unity-network-protocol 스킬 참고).
- 템플릿에 대응되는 게 없는 완전히 새로운 기능일 때만 아래 2~8번 패턴을 따라 처음부터 작성한다.

## 2. VContainer — DI 등록

- 전역 등록은 `LifetimeScope.Configure(IContainerBuilder builder)`에서 하되, 관심사별로 `ConfigureLogging(builder)`, `ConfigureMessagePipe(builder)`처럼 **private/protected 메서드로 쪼갠다.** 하나의 Configure가 모든 걸 다 하지 않도록 하는 이유는, 나중에 로깅만 바꾸거나 파생 LifetimeScope에서 특정 부분만 override하기 쉽게 하기 위함이다.
- **MonoBehaviour는 메서드 주입**을 쓴다:
  ```csharp
  [Inject]
  public void Construct(IPublisher<SomeEvent> publisher, ILogger<MyManager> logger)
  {
      _publisher = publisher;
      _logger = logger;
  }
  ```
  MonoBehaviour는 생성자를 직접 호출할 수 없어서 이 패턴이 필요하다.
- **순수 C# 클래스(서비스, 프로바이더 등)는 생성자 주입**을 쓴다. 의존성이 생성자 시그니처에 드러나고 `readonly` 필드로 받을 수 있어, 주입 누락이 런타임 null이 아니라 컨테이너 빌드 시점 오류로 드러난다.
- **런타임에 생성하는 프리팹은 `IObjectResolver.Instantiate(prefab, parent)`로 만든다.** `Object.Instantiate`로 만든 오브젝트에는 `[Inject]`가 실행되지 않아 `_logger`, publisher 등이 null인 채로 동작한다. 오브젝트 풀(18번)도 풀을 채울 때 이 메서드로 생성한다. 이미 만들어진 오브젝트에 주입해야 하면 `IObjectResolver.InjectGameObject(go)`를 쓴다.

## 3. UniTask — 비동기 처리

- Fire-and-forget 초기화/연출은 `async UniTaskVoid` + 호출부에서 `.Forget()`:
  ```csharp
  protected virtual async UniTaskVoid InitializeAsync()
  {
      await DoSomethingAsync(this.GetCancellationTokenOnDestroy());
  }
  // 호출부: InitializeAsync().Forget();
  ```
  (참고: `GameManagerBase` 파생 클래스에서 세팅 완료 후 초기화가 필요한 경우, 직접 로드하기보다 `protected override void OnSettingsLoaded(Settings loadedSettings)`를 override하여 처리한다.)
- 취소 토큰은 기본적으로 `this.GetCancellationTokenOnDestroy()`를 쓴다. 오브젝트가 파괴되면 자동으로 취소되어 별도 정리 코드가 필요 없다.
- 사용자가 도중에 취소할 수 있는 흐름(페이드, 연출 등)은 별도 `CancellationTokenSource`를 만들어 관리한다. 파괴 토큰과 링크하면 오브젝트 파괴 시 자동 취소와 수동 취소(`_fadeCts.Cancel()`) 둘 다 동작한다. `SoundManager.FadeOutBGM` / `FadeOutAsync`가 표준 형태다:
  ```csharp
  public void StartFade(float duration)
  {
      CancelFade(); // 이전 흐름을 먼저 취소·해제한다
      _fadeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
      FadeAsync(duration, _fadeCts).Forget(); // 토큰이 아니라 CTS 객체를 넘긴다
  }

  private async UniTaskVoid FadeAsync(float duration, CancellationTokenSource cts)
  {
      try { await ...(cts.Token); }
      catch (OperationCanceledException) { ... }
      finally
      {
          // 필드가 아직 자기 CTS일 때만 정리한다
          if (_fadeCts == cts)
          {
              _fadeCts.Dispose();
              _fadeCts = null;
          }
      }
  }
  ```
  `finally`에서 무조건 `_fadeCts?.Dispose(); _fadeCts = null;`을 하면 안 된다. 페이드 A 진행 중 페이드 B가 시작되면, 취소된 A의 `finally`가 뒤늦게 실행되면서 B의 CTS를 폐기해 B가 취소 불가능한 상태가 되거나 `ObjectDisposedException`이 난다.
- **다중 대기 태스크 캐싱은 `UniTask` 대신 `Task`를 사용한다**: `UniTask`는 구조체 기반이라 awaiter를 한 번만 등록할 수 있다. 특히 **로드가 아직 진행 중인 상태에서** 여러 소비자가 같은 `UniTask<T>`를 동시에 `await`하면 continuation이 중복 등록되어 `InvalidOperationException("Already continuation registered")`가 발생하며, `.Preserve()`로도 이 경우는 해결되지 않는다. 부팅 시 여러 매니저가 같은 리소스를 동시에 요청하는 상황이 정확히 여기 해당한다. 따라서 로더/다운로더/설정 제공자는 다중 awaiter를 기본 지원하는 `Task<T>`로 캐싱한다 — 키별 캐시는 `Dictionary<string, Task<T>>`(`SoundManager`의 `_activeDownloads`), 단일 리소스는 `Task<T>` 필드 하나(`AppSettingsProvider`의 `_loadTask`)가 그 패턴이다.

## 4. MessagePipe — 이벤트

- 전역 브로커는 LifetimeScope에서 `builder.RegisterMessageBroker<TEvent>(options)`로 등록한다.
- 발행은 `IPublisher<TEvent>`, 구독은 `ISubscriber<TEvent>`를 주입받아 사용한다.
- 구독은 반드시 `IDisposable`을 받아 `OnDestroy` 등에서 해제한다. 구독 해제를 빼먹으면 파괴된 오브젝트가 이벤트를 계속 받으려다 예외가 난다.

## 5. R3 — 반응형 상태

- 외부에서 관찰해야 하는 상태(체력, 스코어, UI 바인딩 대상 등)는 일반 필드 대신 `ReactiveProperty<T>`로 노출한다.
- 구독은 `.Subscribe(...)`가 반환하는 `IDisposable`을 모아서(`DisposableBag` 등) 오브젝트 파괴 시 한 번에 해제한다. MessagePipe 구독과 동일하게, 해제를 빼먹지 않는 게 핵심이다.

## 6. ZLogger.Unity — 로깅

- 필드로 `ILogger<T> _logger`를 주입받는다.
- 로그를 남길 때는 항상 이 형태를 따른다:
  ```csharp
  if (_logger != null) _logger.ZLogWarning($"[MyManager] 대상이 null이라 처리를 건너뜀.");
  ```
  - `[클래스명]` 태그를 메시지 맨 앞에 붙인다 — 여러 매니저의 로그가 한 콘솔에 섞여도 어디서 난 건지 바로 구분하기 위함.
  - null이 발생할 수 있는 지점(Fallback이 필요한 지점)마다 `_logger != null` 체크 후 경고/에러 로그를 남긴다. 로그 없이 조용히 return하지 않는다.
- **if로 조건/참조를 검사할 때, 조건 실패가 "정상적으로 자주 발생하는 no-op"이 아니라면 실패 분기(else)에 로그를 남긴다.** `if (someRef) { ... }`처럼 성공 분기만 작성하고 else를 생략하면, 조건이 실패했을 때 아무 흔적 없이 조용히 아무 일도 안 일어난다 — 원인 파악이 콘솔 로그가 아니라 코드 리딩으로만 가능해진다.
  ```csharp
  // 씬 참조(MonoBehaviour)는 암시적 bool, 순수 C# 객체인 _logger는 명시적 null 비교.
  if (flowController)
  {
      flowController.ShowCompletePanel();
  }
  else if (_logger != null)
  {
      _logger.ZLogWarning($"[ResultVideoPanel] flowController is null. CompletePanel will not fade in.");
  }
  ```
  실제로 `ResultVideoPanel`에서 `flowController`가 씬에 연결되지 않았는데 else 로그가 없어서, 영상 재생이 끝나도 CompletePanel이 페이드인되지 않는 원인을 로그 없이 코드까지 뒤져서 찾아야 했던 사례가 있었다.
- **`UnityEngine.Debug.Log` 계열은 쓰지 않는다.** 모든 로그는 ZLogger를 거친다. 예외는 아래 세 경우뿐이다.
  - **`Editor/` 폴더의 에디터 전용 코드**: ZLogger DI 컨테이너가 없는 환경이다.
  - **로거를 받을 수 없거나 받지 못했을 때의 대체 출력**: 정적 유틸리티나 로거를 선택 인자로 받는 클래스는 `ILogger`를 인자로 받아 있으면 ZLogger로, 없으면 `Debug.Log`로 남긴다(`JsonLoader`, `LogRetentionService` 패턴). 로거 없이 조용히 넘어가지 않는 것이 핵심이다.
  - **DI 실패 자체를 알릴 때**: `_logger`가 주입되지 않았으면 위 패턴으로는 아무 흔적도 남지 않으므로, 템플릿(`GameManagerBase`, `InactivityTimer`, `ApiManagerBase`)처럼 `Debug.LogError`로 원인과 확인할 등록 코드를 남긴다:
  ```csharp
  if (_logger == null)
  {
      Debug.LogError("[MyManager] Dependencies were not injected. Check that RegisterComponentInHierarchy<MyManager>() is registered on the LifetimeScope.");
  }
  ```
- 로그 레벨: 복구 가능한 이상 상황은 `ZLogWarning`, 기능이 실패한 경우는 `ZLogError`, 정상 흐름 기록은 `ZLogInformation`.

## 7. ZString — 문자열 조합

- 런타임에 반복적으로 실행되는 문자열 연결(로그 메시지의 보간 문자열 자체는 예외)이나 경로/URI 조합은 `+`나 `string.Format` 대신 `ZString.Concat(...)` / `ZString.Format(...)`을 쓴다. GC 할당을 줄이기 위함이다.
  ```csharp
  string uri = ZString.Concat("file://", path);
  ```

## 8. DOTween — 트윈/연출

- 템플릿도 DOTween을 표준으로 쓴다. `HuliacDev.UI.FadeManager`와 `SoundManager`의 페이드가 이미 `DOFade(...).SetUpdate(true)` + UniTask 연동으로 구현되어 있으므로, 새 연출도 같은 패턴으로 작성한다. 화면/볼륨 페이드 자체는 여전히 새로 만들지 않고 해당 매니저를 호출한다(1번 재사용 원칙).
- 새로 만드는 연출에서 `Update()` 안의 수동 `Lerp` 누적이나 `WaitForSeconds` 코루틴으로 위치/색/알파를 직접 보간하지 않는다. 그런 코드는 DOTween 한 줄로 대체된다:
  ```csharp
  await _panel.DOAnchorPosY(0f, 0.3f).SetEase(Ease.OutCubic);
  ```
- **UniTask와 연동해서 await 한다** (3번의 취소 토큰 규칙을 그대로 따른다). `UNITASK_DOTWEEN_SUPPORT` define이 켜져 있어 `DOTweenAsyncExtensions`를 쓸 수 있다. 대기는 `ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: ...)`로 통일한다 — 템플릿 표준 패턴이고, `WithCancellation`과 달리 취소 시 동작(`TweenCancelBehaviour`)을 연출별로 지정할 수 있다.
- **`SetUpdate(true)`는 연출 성격에 따라 구분한다.** `Time.timeScale`을 무시하는 옵션이므로, 페이드/일시정지 메뉴/로딩처럼 timeScale이 0이어도 돌아야 하는 UI·시스템 연출에만 붙이고, 일시정지·슬로모션을 따라야 하는 게임플레이 연출에는 붙이지 않는다:
  ```csharp
  // UI/시스템 연출 (timeScale 0에서도 동작해야 함)
  await _canvasGroup.DOFade(1f, 0.3f).SetUpdate(true)
      .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, this.GetCancellationTokenOnDestroy());

  // 게임플레이 연출 (일시정지/슬로모션을 따라야 함) — SetUpdate 없이
  await transform.DOMove(target, 1f)
      .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, this.GetCancellationTokenOnDestroy());
  ```
  `yield return tween.WaitForCompletion()` 같은 코루틴 대기는 쓰지 않는다.
- **생성한 트윈은 반드시 수명을 묶는다.** 4번(MessagePipe 구독 해제), 5번(R3 구독 해제)과 같은 이유로, 해제를 빼먹으면 파괴된 오브젝트를 트윈이 계속 건드리다 예외가 난다.
  ```csharp
  transform.DOMove(target, 1f).SetLink(gameObject);
  ```
  묶는 방법은 두 가지이고 둘 중 하나는 반드시 쓴다.
  - **await 하지 않는 트윈**은 `SetLink(gameObject)`로 오브젝트 파괴 시 자동 Kill 되게 한다.
  - **await 하는 트윈**은 `ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token)`에 `this.GetCancellationTokenOnDestroy()`(또는 그것과 링크된 CTS)를 넘기면 그 자체로 수명이 묶인다. 파괴·취소 시 트윈이 Kill되고 await도 함께 끊기므로 `SetLink`를 덧붙일 필요가 없다 — `FadeManager`와 `SoundManager`의 페이드가 이 형태다.

  무한 반복(`SetLoops(-1)`) 트윈처럼 도중에 직접 멈춰야 하는 건 `Tween` 참조를 필드로 들고 있다가 `_tween?.Kill()`로 정리한다.
- 트윈 대상이 없는 순수 수치 보간이나 지연 실행은 빈 GameObject를 만들지 말고 `DOVirtual.Float(...)` / `DOVirtual.DelayedCall(...)`을 쓴다.
- **무료판이라 TextMeshPro 숏컷이 없다.** `DOText`, TMP `DOColor`/`DOFade`는 Pro 전용이라 이 프로젝트에서 컴파일되지 않는다. TMP 텍스트 연출이 필요하면 `DOVirtual.Float`로 값을 보간해 콜백에서 직접 대입한다.

## 9. 리플렉션 최소화

런타임 코드에서 리플렉션(`System.Reflection`, `Type.GetType`, `GetMethod`/`GetField`/`Invoke`, `Activator.CreateInstance`, `dynamic`)을 쓰지 않는다. 이유는 세 가지다.

- IL2CPP 빌드에서 코드 스트리핑에 걸려 에디터에서는 되는데 빌드에서만 깨진다. 재현이 어려운 종류의 버그다.
- 컴파일 타임 검증이 사라진다. 문자열로 참조한 멤버는 이름을 바꿔도 컴파일러가 잡아주지 않는다.
- 매 호출마다 할당과 조회 비용이 든다. ZString(7번)으로 GC를 줄이는 것과 정반대 방향이다.

이 스택에는 리플렉션이 필요할 만한 자리에 이미 대안이 있다.

- 타입으로 구현체를 찾아 생성 → VContainer 주입(2번)
- 다른 시스템의 메서드를 이름으로 호출 → MessagePipe 이벤트(4번) 또는 인터페이스
- 상태 변화 감지 → R3 `ReactiveProperty`(5번)
- JSON 역직렬화 → `HuliacDev.Utils.JsonLoader` 재사용(1번)
- 인스펙터 노출 → `[SerializeField]`

`SendMessage`, `Invoke("MethodName", ...)`, `StartCoroutine("MethodName")`처럼 문자열로 메서드를 호출하는 Unity API도 같은 이유로 쓰지 않는다.

예외는 `Editor/` 폴더의 에디터 전용 도구·일회성 디버그 코드, 그리고 `Tests/` 폴더의 테스트 코드다. 둘 다 빌드에 포함되지 않으므로 스트리핑 문제가 없다. 다만 `Tests/`에서 private 필드 검증에 새 리플렉션 코드를 추가하지는 않는다 — 이미 `NonPublic` 리플렉션을 쓰는 기존 헬퍼가 있으면 그걸 재사용하고, 새로 필요하면 가능한 한 공개 상태/이벤트로 노출하는 쪽을 먼저 고려한다(13번 테스트 작성 기준 참고). 그래도 공개 API가 있으면 그쪽을 먼저 쓴다.

기존 코드에서 리플렉션을 발견하면 조용히 남겨두지 말고, 위 대안 중 무엇으로 바꿀 수 있는지 사용자에게 알린다. 다만 CLAUDE.md의 "수술적 변경" 원칙에 따라 요청받지 않은 리팩터링을 임의로 수행하지는 않는다.

## 10. 메서드 분리 기준

한 메서드가 여러 책임(캐시 확인 → 다운로드 → 적용 같은)을 한 번에 처리하지 않도록, 단계별로 private 메서드를 쪼갠다. SoundManager의 `LoadAndPlayAsync → DownloadAndCacheClipAsync → ExecuteDownloadAsync` 체인이 예시다. 기준은 "이 메서드 이름만 보고 무슨 일을 하는지 한 문장으로 설명할 수 있는가" — 안 되면 쪼갠다.

## 11. 주석 규칙 (이 스택 코드에 한함)

프로젝트 전역 규칙은 "주석은 최소화"지만, 이 프로젝트의 매니저/서비스 클래스는 이미 모든 메서드에 한국어 summary 주석이 달려 있다 (실측 컨벤션). 이 스킬이 적용되는 코드에서는 그 기존 컨벤션을 따른다:

- public/protected/private 구분 없이 **모든 메서드**에 `/// <summary>...</summary>` 작성. `<param>`, `<returns>` 태그는 쓰지 않는다.
- Summary는 이 메서드가 **어떤 역할을 하는지**를 한 문장으로 작성한다 (예: "프레임 단위 보간을 통해 볼륨을 줄이는 페이드아웃 핵심 로직").
- 이모티콘, 특수문자 없이 평서형으로 끝맺는다.

## 12. 상수 중앙 관리 (씬 이름 등)

여러 파일에서 참조하는 문자열 상수 — 특히 **씬 이름**, StreamingAssets 파일 이름처럼 "식별자" 성격의 값 — 은 각 클래스에 `[SerializeField] private string xxxSceneName = "..."` 기본값이나 리터럴로 흩어놓지 않고, 프로젝트 쪽 `App.Constants` 같은 **static 클래스 한 곳에 const로 모아** 참조한다. 씬 이름과 파일 이름은 프로젝트마다 다른 식별자이므로 이 클래스는 **프로젝트 어셈블리에 두고, 템플릿 패키지(`HuliacDev.*`)에는 넣지 않는다.**

- 씬을 리네임하면 `Constants.Scenes` 한 줄만 바꾸면 모든 참조가 따라온다.
- 씬 이름을 SerializeField로 두면 코드 기본값과 씬에 직렬화된 값이 이원화되어, 씬 파일에 낡은 값이 남은 채로 조용히 잘못된 씬을 로드하는 사고가 난다. 실제로 씬을 다시 번호 매긴 뒤 `resultSceneName`, `outroSceneName`, `gameSceneName`에 직렬화되어 있던 예전 이름이 갱신되지 않아, 존재하지 않는 씬을 로드하려던 버그가 있었다. 그래서 씬 이름은 SerializeField를 제거하고 코드에서 `Constants.Scenes.Xxx`를 직접 참조한다.
- 관심사별 중첩 static 클래스로 묶는다:
  ```csharp
  public static class Constants
  {
      public static class Scenes
      {
          public const string Title = "0_Title";
          public const string Game = "3_Game";
          // ...
      }

      public static class Files { public const string RfidMappings = "RfidMappings.json"; }
  }
  ```
- 값을 어디에 둘지는 자료형(string/int/float)이 아니라 "누가, 언제 바꾸는가"로 정한다. 숫자라도 마스터·슬레이브가 맞춰 쓰는 포트 번호는 `Constants`, 문자열이라도 스토리 문구는 ScriptableObject다:

  | 값의 성격 | 둘 곳 | 예 |
  |---|---|---|
  | 코드끼리 맞춰 쓰는 식별자 (개발자가 코드와 함께 바꿈) | `Constants` (const) | 씬 이름, 파일 이름, API 경로, 패킷 접두어, 프로토콜 포트 |
  | 한 컴포넌트에서만 쓰는 튜닝 값 | `[SerializeField]` 기본값 | 특정 패널의 페이드 시간, 타이핑 속도 |
  | 여러 컴포넌트가 함께 쓰는 튜닝 값·콘텐츠 (에디터에서 조정) | ScriptableObject | 브러시 굵기 단계, 팔레트 색상, 연출 타이밍 세트, 스토리 문구 |
  | 빌드 후 현장에서 바꿔야 하는 값 | StreamingAssets JSON (`Settings.json` 등) | 서버 IP/포트, 타임아웃, 자동 복귀 대기 시간 |

- 한 컴포넌트에서만 쓰는 튜닝 값은 기본적으로 `[SerializeField]`로 두고, 아래 중 하나에 해당할 때만 ScriptableObject로 옮긴다. 처음부터 SO로 만들지 않는다 (에셋 생성·인스펙터 연결·null 처리 비용만 늘어난다):
  - Play 모드에서 반복 조정해야 하는 값이 많다 (SerializeField는 Play 종료 시 되돌아가지만 SO는 에셋에 남는다).
  - 값 묶음을 프리셋으로 통째로 교체해야 한다 (예: 전시별·연출 속도별 설정).
  - 같은 컴포넌트가 여러 씬·프리팹에 배치되어 값을 한 번에 바꿔야 한다.
  - 조정값이 많아 인스펙터에서 참조 필드와 섞여 관리가 어렵다.
- 식별자를 ScriptableObject로 옮기지 않는다. 에셋에 직렬화된 값과 코드가 이원화되어 위의 씬 이름 사고가 똑같이 재발하고, 인스펙터 연결 누락이라는 null 실패 지점도 늘어난다.
- ScriptableObject는 빌드 후 파일로 수정할 수 없고, 런타임에 바꾼 값은 빌드에서는 재시작 시 사라지며 에디터 Play 모드에서는 에셋에 그대로 저장되므로 런타임 상태 저장소로 쓰지 않는다.

## 13. 테스트 작성 기준 (UTF)

이 스킬로 매니저/시스템 클래스를 스캐폴딩하거나 수정할 때, 아래 조건에 해당하면 `Tests/Runtime`에 대응하는 `[UnityTest]`를 같이 작성한다. 조건에 해당하지 않으면 테스트를 만들지 않는다 — 모든 클래스에 테스트를 강제하지 않는다.

**테스트가 필요한 경우:**
- `Time.timeScale`, `SetUpdate(true)`처럼 타이밍/일시정지에 영향을 받는 로직 (예: 페이드, 연출)
- 상태 플래그(`_isTransitioning` 등)로 중복 호출을 막는 로직 — 플래그가 해제되지 않으면 이후 호출이 전부 무시되는 소프트락 위험이 있는 코드
- 여러 곳에서 동시에 호출될 수 있는 캐싱/중복 방지 로직 (`SoundManager`의 `_activeDownloads` 패턴 등)
- 과거에 버그가 발생했던 지점을 수정하는 경우 (회귀 방지)

**테스트가 필요 없는 경우:**
- 단순 getter/setter, DI 등록 코드, 데이터 클래스
- Inspector 값 대입만 하는 초기화 코드

**작성 패턴** (`FadeManagerTests.cs` 표준을 따른다):
```csharp
[UnityTest]
public IEnumerator 설명은_한글_평서형으로() => UniTask.ToCoroutine(async () =>
{
    await _target.SomeAsync().AwaitWithRealtimeTimeout();
    Assert.IsFalse(GetIsTransitioning(), "플래그가 해제되지 않아 이후 호출이 무시됨");
});
```
- 비동기 완료 대기는 반드시 실시간 타임아웃을 건다. 매번 `UniTask.WhenAny(task, UniTask.Delay(..., DelayType.UnscaledDeltaTime))`를 직접 작성하지 않고 `Tests/Runtime/TestTaskExtensions.cs`의 공통 확장 메서드 `AwaitWithRealtimeTimeout()`(`UniTask`/`UniTask<T>` 양쪽 오버로드 제공)를 재사용한다. 가드가 없으면 결함이 있는 구현에서 테스트가 "실패"가 아니라 "무한 대기"로 멈춰 테스트 러너 전체를 막는다.
- `[TearDown]`에서 `Time.timeScale` 등 건드린 전역 상태를 반드시 원복한다.
- private 필드 검증에 새 리플렉션 코드를 추가하지 않는다(9번 규칙과 동일한 이유). 이미 `NonPublic` 리플렉션이 쓰인 기존 헬퍼가 있으면 재사용하되, 새로 만들 경우 가능하면 공개 상태/이벤트로 노출하는 걸 우선 고려한다.

## 14. 구조체 메모리 정렬 및 패딩 (Data Alignment & Padding)

C#의 참조 타입(`class`)은 CLR이 `[StructLayout(LayoutKind.Auto)]`를 통해 런타임에 멤버를 재배치해 패딩을 줄여주지만, 값 타입(`struct`)은 기본값이 `[StructLayout(LayoutKind.Sequential)]`이므로 프로그래머가 작성한 순서 그대로 메모리에 배치된다.

- **원인**: CPU는 메모리 버스 단위(64비트는 8바이트)로 데이터를 읽으며, 각 기본 타입은 자신의 크기 배수 주소에 놓여야 한다(Unaligned Access 방지). 작은 타입 뒤에 큰 타입이 오면 그 사이에 패딩 바이트(공백)가 강제 삽입된다.
- **규칙**: 상태 저장용 또는 대량 인스턴스가 생성되는 `struct`를 정의할 때는 멤버 변수를 **바이트 크기 내림차순(8B → 4B → 2B → 1B)**으로 선언한다:
  ```csharp
  // 비권장 (패딩 9바이트 낭비 -> 총 24바이트)
  public struct BadData
  {
      public byte id;     // 1B + 패딩 3B
      public int hp;      // 4B
      public byte level;  // 1B + 패딩 7B
      public double exp;  // 8B
  }

  // 권장 (패딩 2바이트 최소화 -> 총 16바이트)
  public struct GoodData
  {
      public double exp;  // 8B
      public int hp;      // 4B
      public byte id;     // 1B
      public byte level;  // 1B + 끝 패딩 2B (8의 배수 정렬)
  }
  ```
- 특히 C++ 네이티브 플러그인, Compute Shader(HLSL 버퍼), Job System/DOTS에 전달되는 구조체는 양쪽 레이아웃이 어긋나면 메모리 깨짐이 발생하므로 반드시 이 규칙을 준수한다.
- **단, 네트워크/하드웨어 패킷 구조체에는 이 크기순 재정렬 규칙을 적용하지 않는다.** 패킷의 필드 순서는 상대방(서버, 임베디드 장치)과 합의한 와이어 포맷이 결정하므로, 패딩을 줄이겠다고 순서를 바꾸면 바이트 오프셋이 어긋나 좌표나 센서값이 쓰레기로 해석된다. 패킷은 `Pack = 1`로 패딩을 아예 없애고 프로토콜이 정한 순서를 그대로 따른다(unity-network-protocol 스킬 2번 참고).

## 15. 비동기 스레드 안전성과 메인 스레드 전환 (UniTask)

Unity 엔진의 네이티브 객체(`Transform`, `GameObject`, `Component`, UI 등)는 락(Lock) 없이 설계되어 멀티스레드 동시 접근 시 데이터 레이스 및 크래시가 발생하므로, 메인 스레드에서만 조작해야 한다.

- 무거운 연산(수학 계산, 파일 I/O, 대용량 JSON 파싱 등)을 `UniTask.RunOnThreadPool`이나 백그라운드 태스크로 처리하는 것은 권장된다.
- **규칙**: 백그라운드 스레드에서 작업이 끝난 후 Unity 엔진 객체를 참조/수정해야 할 때는 반드시 `await UniTask.SwitchToMainThread()`를 통해 메인 스레드로 명시적 복귀한 뒤 조작한다:
  ```csharp
  // 백그라운드 워커 스레드에서 무거운 데이터 처리
  await UniTask.RunOnThreadPool(() =>
  {
      ProcessHeavyData();
  });

  // Unity API 호출 전 메인 스레드로 안전하게 복귀
  await UniTask.SwitchToMainThread(cancellationToken: this.GetCancellationTokenOnDestroy());
  transform.position = newPosition;
  ```
- **멀티스레드 공유 플래그와 원자적 연산**:
  - 백그라운드 스레드와 메인 스레드가 함께 참조하는 실행 플래그는 CPU 레지스터 캐싱 및 컴파일러의 명령어 재배치(Out-of-Order Execution)를 방지하기 위해 `private volatile bool _isRunning;` 형태로 선언한다.
  - 스레드 간 동시 카운트 증감이나 상태 플래그 교체는 락(Lock) 없이 고속으로 안전하게 처리하기 위해 `Interlocked.Increment(ref _counter)` 또는 `Interlocked.Exchange(...)` 등 원자적(Atomic) 연산을 사용한다.
- 소켓 수신 루프처럼 반복적으로 백그라운드↔메인 스레드를 오가는 구체적인 패턴은 unity-network-protocol 스킬 4번을 참고한다.

## 16. 캐시 지역성(Cache Locality)과 자료구조 선택

현대 하드웨어는 CPU 연산 속도와 메인 RAM 접근 속도의 격차(메모리 벽)를 극복하기 위해 64바이트 캐시 라인(Cache Line) 단위로 데이터를 CPU 캐시(L1/L2/L3)로 퍼온다.

- **LinkedList 지양**: `LinkedList<T>`는 이론상 중간 삽입/삭제가 $O(1)$이지만, 노드들이 힙(Heap) 곳곳에 분산 할당되어 순회 시 포인터를 타고 갈 때마다 100% 캐시 미스(Cache Miss)가 발생하고 노드당 추가 힙 할당(GC)이 유발된다. 실무에서는 특별한 이유가 없는 한 사용하지 않는다.
- **연속 메모리 우선**: 데이터가 메모리에 일렬로 연속 배치되는 `T[]` (배열) 또는 `List<T>`를 기본으로 사용한다. 인덱스 0번에 접근할 때 인접 요소들이 64바이트 캐시 라인에 함께 적재(공간 지역성, Spatial Locality)되므로 순회 속도가 압도적이다.
- **Swap-Back 삭제 패턴**: 빈번한 중간 요소 삭제가 발생하고 요소의 순서 유지가 중요하지 않은 컬렉션은, $O(N)$ 메모리 이동 복사를 피하기 위해 맨 뒤 요소를 삭제 위치로 덮어쓰고 마지막을 제거하는 패턴을 사용한다:
  ```csharp
  public static void RemoveAtSwapBack<T>(List<T> list, int index)
  {
      int lastIndex = list.Count - 1;
      list[index] = list[lastIndex];
      list.RemoveAt(lastIndex);
  }
  ```
- **공간 분할(Spatial Hashing) 활용 ($O(N^2)$ 전수 검사 금지)**: 다수의 오브젝트(관람객 인터랙션, 센서 마커, 파티클 등) 간의 근접 검사나 충돌 탐색 시 모든 대상 간의 거리를 이중 루프로 전수 검사하지 않는다. 2D/3D 공간을 그리드 셀로 분할하여 인접한 셀의 대상들만 국소적으로 검사하도록 작성한다.

## 17. 핫패스(Hot Path) 성능 및 가상 함수/인라인 최적화

C#의 `virtual` 메서드나 인터페이스 호출은 런타임에 객체의 메서드 테이블(vtable/메타데이터)을 조회하는 간접 참조 비용이 들며, 컴파일러의 가장 강력한 최적화인 **인라인화(Inlining)**를 차단한다.

- 매니저, 서비스, 생명주기 이벤트 등 일반적인 시스템 구조에서는 DI와 유지보수를 위해 VContainer 인터페이스 및 가상 메서드를 표준으로 사용한다 (2번 원칙).
- **규칙**: 매 프레임 수백~수천 번 이상 호출되는 핫패스(Hot Path, 예: `Update()` 내의 물리 연산 루프, 대량 파티클/엔티티 수학 계산)에서는 불필요한 단일 구현 인터페이스나 깊은 가상 메서드 체인을 피하고 구체 클래스/구조체 직접 호출을 유지하여 JIT/IL2CPP 인라인 최적화를 보존한다.

## 18. Zero-GC 및 박싱 방지 (GC Spike 방지 규칙)

매 프레임 호출되는 게임플레이 루프에서 발생하는 미세한 힙 할당은 0세대(Gen 0) 가비지로 누적되어 예고 없는 순간적인 프레임 드랍(GC 스파이크)을 유발한다.

- **LINQ 지양 (런타임 루프)**: `.Where()`, `.Select()`, `.ToList()`, `.OrderBy()` 등 `System.Linq` 메서드는 호출할 때마다 내부적으로 이터레이터 객체와 대리자(Delegate)를 힙에 새로 할당한다. 초기화/설정 로드가 아닌 런타임 반복 호출부(`Update`, 빈번한 이벤트 핸들러)에서는 LINQ 대신 단순 `for` 루프나 캐싱된 리스트를 사용한다.
- **박싱(Boxing) 방지**:
  - 값 타입(`int`, `float`, `struct`, `enum`)을 `object`나 제약 없는 인터페이스 타입으로 전달하면 힙에 포장 객체가 생성된다.
  - `enum`을 `Dictionary<TKey, TValue>`의 키로 쓰는 것 자체는 막지 않는다. 핫패스에서 쓰는 딕셔너리가 프로파일러에 GC Alloc으로 잡히면 그때 전용 `IEqualityComparer<TEnum>`을 생성자에 넘긴다.
- **컴포넌트 조회는 미리 찾아 필드에 저장한다**: `TryGetComponent`(0번)도 계층·컴포넌트 목록을 조회하는 호출이므로 `Update`나 빈번한 이벤트 핸들러에서 매번 부르지 않는다. `Awake`에서 한 번 찾아 필드에 저장하고, 가능하면 `[SerializeField]`로 연결해 조회 자체를 없앤다.
- **람다 클로저(Closure) 주의**:
  - 람다식이나 이벤트 리스너 내부에서 바깥 스코프의 로컬 변수를 참조(캡처)하면, 컴파일러가 해당 변수를 담기 위한 임시 클래스 인스턴스를 힙에 매번 할당한다.
  - 반복 호출되는 콜백에는 외부 변수를 캡처하지 않는 정적 람다(`static (x) => ...`)를 쓰거나 상태를 매개변수로 명시적 전달한다.
- **오브젝트 풀링**: 빈번하게 생성/파괴되는 투사체, 대미지 텍스트, 파티클, UI 목록 아이템은 `Instantiate`/`Destroy` 대신 풀링을 적용하여 힙 단편화와 GC 부하를 억제한다.
- **임시 버퍼 풀링 (`ArrayPool<T>`)**: 반복적으로 실행되는 비동기 I/O 패킷 파싱이나 대량 수학 연산에서 `new byte[4096]`처럼 임시 배열을 힙에 반복 생성하지 않는다. `System.Buffers.ArrayPool<T>.Shared.Rent(size)`로 대여한 뒤 `finally`에서 반드시 `Return`한다. 수신 버퍼처럼 수명이 명확한 것은 루프 밖에서 한 번 할당해 재사용하는 것으로 충분하다.
  - `NativeArray<T>(count, Allocator.Temp)`는 **동기 루프 안에서 그 프레임 안에 다 쓰고 버릴 때만** 쓴다. Temp는 프레임·스레드 스코프 얼로케이터라 `await`를 사이에 끼면 다음 프레임이나 다른 스레드에서 재개될 때 이미 해제된 메모리를 가리키게 되어 "The NativeArray has been deallocated" 오류나 네이티브 메모리 손상이 발생한다(세이프티 체크가 꺼진 빌드에서는 조용히 깨진다). 비동기 경로에는 `ArrayPool<T>`나 `Allocator.Persistent`를 쓴다.

## 19. UI 렌더링 및 캔버스 최적화 (Overdraw & Rebuild 방지)

UI(UGUI)는 CPU의 메시 재생성(Rebuild)과 GPU의 픽셀 덮어쓰기(Overdraw) 양쪽에서 병목의 주원인이 된다.

- **Raycast Target 비활성화**: 클릭이나 터치 입력을 받지 않는 모든 `Image`, `TextMeshProUGUI`는 반드시 `Raycast Target` 체크를 끈다. 켜져 있으면 사용자가 화면을 터치할 때마다 `GraphicRaycaster`가 해당 컴포넌트들의 경계를 불필요하게 전부 순회 검사하여 CPU 스파이크를 일으킨다.
- **캔버스 분리 (Dynamic vs Static)**:
  - 캔버스 내의 UI 요소가 단 하나라도 이동/크기변경/텍스트수정되면, 해당 캔버스에 속한 모든 UI 요소의 버텍스 메시가 처음부터 전부 재생성(Canvas Rebuild)된다.
  - 매 프레임 또는 자주 갱신되는 UI(체력 바, 타이머, 미니맵 아이콘 등)는 별도의 하위 `Canvas` 컴포넌트를 붙여 정적인 배경/프레임 UI와 메시 재생성 영역을 분리한다.
- **투명 패널 오버드로우 방지**:
  - 단순 레이아웃 정렬이나 클릭 차단용으로 투명한 패널을 만들 때, 알파가 0인 `Image` 컴포넌트를 화면 전체에 깔아두지 않는다. 화면에 보이지 않아도 GPU는 해당 영역의 픽셀 셰이더를 전부 실행(오버드로우)한다.
  - 레이아웃 정렬에는 컴포넌트 없는 빈 `RectTransform`을 사용하고, 광선 차단이 목적이면 `Graphic`을 상속해 `OnPopulateMesh(VertexHelper vh)`에서 `vh.Clear()`만 호출하는 빈 그래픽 컴포넌트를 쓴다. 정점을 하나도 내보내지 않으므로 레이캐스트는 받으면서 그리는 픽셀은 없다. (`Graphic`에는 `[RequireComponent(typeof(CanvasRenderer))]`가 붙어 있어 CanvasRenderer 자체를 떼어낼 수는 없다 — 떼는 게 아니라 그릴 메시를 비우는 것이 핵심이다.)
- **UI 스프라이트 Mipmap 비활성화**:
  - UGUI/HUD에 사용되는 모든 2D 스프라이트 및 UI 텍스처는 인스펙터 Import Settings에서 `Generate Mip Maps`를 반드시 끈다.
  - UI는 카메라와의 거리가 일정하여 밉맵 축소본을 참조할 일이 없으므로, 켜둘 경우 33%의 불필요한 VRAM 낭비 및 특정 해상도에서 UI 텍스트나 아이콘이 뿌옇게 흐려지는(Blur) 현상이 발생한다.

## 20. 부동소수점(float) 연산 및 비교 규칙 (IEEE 754)

컴퓨터는 소수를 2진수 비트로 근사 표현하므로(`0.1f + 0.2f != 0.3f`), 부동소수점 연산에는 항상 미세한 오차가 존재한다.

- **직접 일치 비교(`==`, `!=`) 금지**: `float` 변수를 `0f`나 특정 목표값과 직접 `==`로 비교하면 영원히 참이 되지 않아 타이머 멈춤, 무한 루프, 이동 상태 전이 실패 등의 버그가 발생한다.
- **권장 비교 방식**:
  - 0f 또는 목표값 도달 검사: `Mathf.Abs(v) < 0.001f`, `Vector3.Distance(current, target) < 0.01f`처럼 **명시적 오차 허용치(Epsilon)**를 둔다.
  - 타이머/게이지 카운트다운: 등호 대신 부등호 사용 (`currentTimer <= 0f`).
  - `Mathf.Approximately(a, b)`는 **`0f`와의 비교에 쓰지 않는다.** 이 함수는 `Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8)`인 상대 오차 방식이라 한쪽이 `0f`면 허용치가 같이 0으로 붕괴해 사실상 `==`와 똑같이 동작한다(`Mathf.Approximately(1e-8f, 0f)`는 `false`). 0이 아닌 두 값의 크기가 비슷할 때만 쓴다.

## 21. 셰이더 및 GPU 연산 최적화 (HLSL / ShaderGraph)

GPU는 32개(또는 64개)의 스레드가 한 묶음으로 동일한 명령어를 실행하는 SIMT(Single Instruction, Multiple Threads / Warp) 구조로 작동한다.

- **분기 다이버전스(Branch Divergence) 방지**:
  - 워프 안에서 분기 결과가 스레드마다 갈리면, 참인 스레드와 거짓인 스레드가 서로의 연산이 끝날 때까지 번갈아 유휴 대기(Idle)하므로 양쪽 분기 비용을 모두 치르게 된다.
  - 모든 `if`가 문제인 것은 아니다. 머티리얼 속성·`uniform`처럼 **모든 픽셀에서 결과가 같은 분기는 다이버전스가 없고**, 아래 예시 같은 단순 대입 분기는 컴파일러가 알아서 분기 없는 선택 명령으로 바꾼다.
  - 피해야 하는 건 **픽셀마다 결과가 달라지는 조건(UV, 텍스처 값, 월드 좌표 등) 아래에 무거운 연산(텍스처 샘플링, 반복문, 복잡한 수식)이 있는 분기**다. 이 경우 `step()`, `lerp()`, `smoothstep()`, `saturate()` 등 내장 수학 함수로 단일 수식을 만들어 분기 없이 계산한다:
    ```hlsl
    // 비권장 (분기 다이버전스 유발)
    float3 color;
    if (val > threshold) color = colorA;
    else color = colorB;

    // 권장 (분기 없는 Branchless 연산)
    float t = step(threshold, val);
    float3 color = lerp(colorB, colorA, t);
    ```

## 22. 게임플레이 아키텍처: 상태 패턴(FSM) 및 명령 패턴(Command)

캐릭터 제어, 몬스터/NPC AI, 또는 전시 체험 시퀀스(대기 → 인식 → 체험 → 결과)를 구현할 때 거대한 `if-else`/`switch` 플래그 스파게티를 지양하고 전용 패턴을 채택한다.

- **상태 패턴 (State Pattern / FSM)**:
  - **FSM은 직접 구현하지 않는다.** `HuliacDev.Core`의 `IState<TContext>` / `StateMachine<TContext>`(주체를 인자로 받는 형태)와 컨텍스트가 필요 없을 때 쓰는 `IState` / `StateMachine`을 그대로 쓴다 (1번 재사용 원칙). 프로젝트마다 `IState`를 새로 선언하면 같은 걸 매번 다시 만들게 된다.
  - 이 스택에서 FSM이 지켜야 하는 계약은 다음과 같다. 템플릿 구현은 이 계약을 만족해야 하고, 만족하지 않으면 템플릿을 고친다.
    - 상태는 `Enter` / `Update` / `Exit` 세 생명주기를 갖고, 주체(`Player.cs` 등)는 직접 행동을 판별하지 않고 현재 상태 객체에 위임만 한다.
    - **동일 상태로의 재진입은 무시한다.** 막지 않으면 `Exit` → `Enter`가 같은 인스턴스에 연속으로 불려 진입 연출이나 타이머가 매 호출마다 초기화된다.
    - **Zero-GC 준수**: 상태를 바꿀 때마다 `new JumpState()`처럼 힙 할당을 발생시키지 않는다. 초기화(`Awake`/`Start`) 시 상태 인스턴스를 미리 만들어 두고 전환 시에는 참조만 교체한다.
    - **상태 변화를 외부에서 관찰해야 하면 R3로 노출한다**(5번). 순수 C# `event`는 쓰지 않는다 — 구독 해제 규약이 R3/MessagePipe와 달라져 해제 누락이 섞인다.
  - 상태를 컨텍스트 인자로 받게 하면 상태 객체가 주체를 필드로 들고 있을 필요가 없어, 인스턴스를 여러 주체가 공유하거나 `static`으로 둘 수 있다:
    ```csharp
    private StateMachine<PlayerController> _stateMachine;
    private readonly IdleState _idleState = new IdleState();
    private readonly JumpState _jumpState = new JumpState();

    private void Awake()
    {
        _stateMachine = new StateMachine<PlayerController>(this, _idleState);
    }

    private void Update() => _stateMachine.Update();

    // 전환은 미리 만들어 둔 인스턴스의 참조 교체로만 일어난다.
    private void OnJumpInput() => _stateMachine.ChangeState(_jumpState);
    ```

- **명령 패턴 (Command Pattern)**:
  - 사용자 입력(키보드, 마우스, 터치, 센서)이나 시스템 요청을 처리할 때 실행할 행동을 `ICommand`(`Execute()`, 필요시 `Undo()`) 객체로 캡슐화한다.
  - **적용 대상**:
    - **입력 버퍼링 (선입력)**: 모션/딜레이 중 입력된 커맨드를 `Queue<ICommand>`에 보관했다가 행동 가능 시점에 즉시 실행하여 씹힘 없는 조작감 구현.
    - **실행 취소 (Undo / Redo)**: 퍼즐, 에디터 도구, 턴제 시스템에서 행동 히스토리를 `Stack<ICommand>`에 저장하여 되돌리기 지원.
    - **네트워크 동기화 & 리플레이**: 전체 좌표 전송 대신 발생한 커맨드 목록만 직렬화하여 전송함으로써 대역폭 최소화 및 리플레이 재현.

## 23. 프로파일링 계측 (ProfilerMarker)

기본 Profiler는 Deep Profile을 켜지 않으면 `Update` 단위까지만 보여 주고, Deep Profile은 부하가 커서 수치가 왜곡된다. 무거운 구간에 이름 붙은 마커를 심어 두면 Deep Profile 없이도 Profiler Hierarchy와 Profile Analyzer에서 그 구간이 따로 보이고, 에이전트도 Unity MCP에서 `ProfilerRecorder`로 마커 이름을 지정해 시간을 읽을 수 있다.

이 스킬로 아래 조건에 해당하는 코드를 작성할 때 마커를 같이 넣는다. 조건에 해당하지 않는 코드에는 넣지 않는다 — 모든 메서드에 마커를 강제하지 않는다(13번 테스트 기준과 같은 방식).

- **마커를 넣는 구간**:
  - 매 프레임 도는 핵심 루프 (17번 핫패스, 대량 엔티티·센서 데이터 처리, 16번 공간 분할 검사)
  - 수신 패킷 디스패치처럼 입력량에 따라 호출 빈도가 달라지는 처리
  - 한 번이지만 프레임을 멈출 수 있는 동기 작업 (대용량 데이터 파싱, 오브젝트 풀 채우기, 씬 진입 직후 초기화)
- **형태**: `static readonly` 필드로 한 번만 만들고 `Auto()` 스코프로 감싼다. 마커 이름은 `클래스명.메서드명`으로 짓는다 — 6번 로그 태그와 같은 이유로, 여러 시스템의 샘플이 섞여도 출처가 바로 드러난다.
  ```csharp
  using Unity.Profiling;

  private static readonly ProfilerMarker UpdateEntitiesMarker = new ProfilerMarker("MyManager.UpdateEntities");

  private void Update()
  {
      using (UpdateEntitiesMarker.Auto())
      {
          UpdateEntities();
      }
  }
  ```
  `ProfilerMarker`와 `Auto()` 스코프는 구조체라 힙 할당이 없어 18번 Zero-GC 규칙과 충돌하지 않고, 프로파일러가 빠진 릴리스 빌드에서는 비용이 사실상 없으므로 배포 코드에 남겨 둔다.
- **`await`를 마커 스코프 안에 넣지 않는다**: 마커의 시작과 끝은 같은 스레드, 같은 프레임 안에서 짝이 맞아야 한다. `using (...Auto())` 안에서 `await`하면 다른 프레임이나 스레드에서 끝이 기록되어 샘플이 어긋난다. 비동기 흐름은 `await` 사이의 동기 구간만 각각 감싼다.



