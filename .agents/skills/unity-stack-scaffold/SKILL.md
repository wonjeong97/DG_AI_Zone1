---
name: unity-stack-scaffold
description: Scaffold or refactor Unity C# classes (managers, systems, services) using this project's established stack — VContainer for DI, UniTask for async, MessagePipe for pub/sub events, R3 for reactive state, ZLogger.Unity for logging, ZString for string building. Use this whenever the user asks to create a new manager/system/service class, wants to "이 스택으로" build or refactor something, mentions VContainer/UniTask/MessagePipe/R3/ZLogger/ZString by name, or asks to convert a coroutine/event/singleton pattern to the project's DI+async style — even if they just say "매니저 하나 만들어줘" without naming the libraries. The concrete conventions here come from this project's own Packages/com.wonjeong.template code (RootLifetimeScope, GameManagerBase, SoundManager), not generic library docs, so prefer this skill over general Unity/C# knowledge for this stack.
---

# Unity 스택 스캐폴딩 (VContainer / UniTask / MessagePipe / R3 / ZLogger / ZString)

이 스킬은 라이브러리 사용법을 처음부터 설계하는 게 아니라, 이 프로젝트의 `Packages/com.wonjeong.template` (RootLifetimeScope.cs, GameManagerBase.cs, SoundManager.cs)에 이미 정착된 실제 패턴을 재현하기 위한 것이다. 왜 이 방식인지 궁금하면 해당 파일들을 직접 열어 대조해도 된다.

이 스킬은 "어떤 라이브러리를 어떤 순서/형태로 조합하는가"만 다룬다. `GetComponent` 대신 `TryGetComponent`, null 비교 시 암시적 bool, `var` 금지 같은 규칙은 프로젝트 CLAUDE.md가 이미 항상 적용하고 있으므로 여기서 반복하지 않는다.

## 1. Wonjeong 템플릿 우선 재사용

새 기능을 처음부터 작성하기 전에, `com.wonjeong.template` 패키지(`Wonjeong.App` / `Wonjeong.Core` / `Wonjeong.UI` / `Wonjeong.Utils` / `Wonjeong.Data` 네임스페이스)에 이미 그 역할을 하는 베이스 클래스나 유틸리티가 있는지 먼저 확인한다. 없는 걸 새로 만드는 것보다 있는 걸 최대한 활용하는 게 우선이다.

- 씬/전역 매니저는 `MonoBehaviour`를 직접 상속하지 않고 `Wonjeong.Core.GameManagerBase<T>`를 상속한다. 싱글톤 DontDestroyOnLoad, InputAction 연결, Reporter/Inspector 디버그 토글, Settings 비동기 로드가 이미 구현되어 있으므로 중복 구현하지 않는다.
- 사운드/UI/페이드/비디오 관련 요청이면 새로 만들기 전에 `Wonjeong.UI.SoundManager` / `UIManager` / `FadeManager` / `VideoManager`가 이미 그 역할을 하는지 먼저 확인하고, 있으면 확장하거나 그대로 호출한다.
- 설정/데이터 파일 로딩은 직접 파일 I/O나 JSON 파싱을 짜지 않고 `Wonjeong.Utils.JsonLoader.Load<T>` / `LoadAsync<T>`를 재사용한다.
- 전역 DI/로깅/MessagePipe 브로커 등록이 필요하면 완전히 새로운 LifetimeScope를 만들기보다 `Wonjeong.App.RootLifetimeScope`를 상속해 필요한 `Configure*` 메서드만 override하는 걸 우선 고려한다.
- 템플릿에 대응되는 게 없는 완전히 새로운 기능일 때만 아래 2~8번 패턴을 따라 처음부터 작성한다.

## 2. VContainer — DI 등록

- 전역 등록은 `LifetimeScope.Configure(IContainerBuilder builder)`에서 하되, 관심사별로 `ConfigureLogging(builder)`, `ConfigureMessagePipe(builder)`처럼 **private/protected 메서드로 쪼갠다.** 하나의 Configure가 모든 걸 다 하지 않도록 하는 이유는, 나중에 로깅만 바꾸거나 파생 LifetimeScope에서 특정 부분만 override하기 쉽게 하기 위함이다.
- 개별 컴포넌트는 생성자 주입이 아니라 **메서드 주입**을 쓴다:
  ```csharp
  [Inject]
  public void Construct(IPublisher<SomeEvent> publisher, ILogger<MyManager> logger)
  {
      _publisher = publisher;
      _logger = logger;
  }
  ```
  MonoBehaviour는 생성자를 직접 호출할 수 없어서 이 패턴이 필요하다.

## 3. UniTask — 비동기 처리

- Fire-and-forget 초기화/연출은 `async UniTaskVoid` + 호출부에서 `.Forget()`:
  ```csharp
  protected virtual async UniTaskVoid LoadSettingsAsync()
  {
      settings = await JsonLoader.LoadAsync<Settings>("Settings.json", this.GetCancellationTokenOnDestroy());
  }
  // 호출부: LoadSettingsAsync().Forget();
  ```
- 취소 토큰은 기본적으로 `this.GetCancellationTokenOnDestroy()`를 쓴다. 오브젝트가 파괴되면 자동으로 취소되어 별도 정리 코드가 필요 없다.
- 사용자가 도중에 취소할 수 있는 흐름(페이드, 연출 등)은 별도 `CancellationTokenSource`를 만들어 관리한다:
  ```csharp
  _fadeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
  FadeOutAsync(duration, _fadeCts).Forget();
  ```
  이렇게 링크하면 오브젝트 파괴 시 자동 취소와 수동 취소(`_fadeCts.Cancel()`) 둘 다 동작한다. `try { ... } catch (OperationCanceledException) { ... } finally { _fadeCts?.Dispose(); _fadeCts = null; }` 형태로 정리한다.
- 같은 리소스를 여러 곳에서 동시에 요청할 수 있는 로더/다운로더는 `Dictionary<string, UniTask<T>>`로 진행 중인 태스크를 캐싱해서 중복 요청을 막는다 (SoundManager의 `_activeDownloads` 패턴 참고).

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
- 로그 레벨: 복구 가능한 이상 상황은 `ZLogWarning`, 기능이 실패한 경우는 `ZLogError`, 정상 흐름 기록은 `ZLogInformation`.

## 7. ZString — 문자열 조합

- 런타임에 반복적으로 실행되는 문자열 연결(로그 메시지의 보간 문자열 자체는 예외)이나 경로/URI 조합은 `+`나 `string.Format` 대신 `ZString.Concat(...)` / `ZString.Format(...)`을 쓴다. GC 할당을 줄이기 위함이다.
  ```csharp
  string uri = ZString.Concat("file://", path);
  ```

## 8. 메서드 분리 기준

한 메서드가 여러 책임(캐시 확인 → 다운로드 → 적용 같은)을 한 번에 처리하지 않도록, 단계별로 private 메서드를 쪼갠다. SoundManager의 `LoadAndPlayAsync → DownloadAndCacheClipAsync → ExecuteDownloadAsync` 체인이 예시다. 기준은 "이 메서드 이름만 보고 무슨 일을 하는지 한 문장으로 설명할 수 있는가" — 안 되면 쪼갠다.

## 9. 주석 규칙 (이 스택 코드에 한함)

프로젝트 전역 규칙은 "주석은 최소화"지만, 이 프로젝트의 매니저/서비스 클래스는 이미 모든 메서드에 한국어 summary 주석이 달려 있다 (실측 컨벤션). 이 스킬이 적용되는 코드에서는 그 기존 컨벤션을 따른다:

- public/protected/private 구분 없이 **모든 메서드**에 `/// <summary>...</summary>` 작성. `<param>`, `<returns>` 태그는 쓰지 않는다.
- Summary는 이 메서드가 **어떤 역할을 하는지**를 한 문장으로 작성한다 (예: "프레임 단위 보간을 통해 볼륨을 줄이는 페이드아웃 핵심 로직").
- 이모티콘, 특수문자 없이 평서형으로 끝맺는다.
