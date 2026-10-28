# TODO

<!-- 이 프로젝트에서 해야 할 일. 사용자가 적고, Claude가 작업하면서 갱신한다(Antigravity는 읽기만 한다). -->
<!-- 형식: - [ ] 할 일 — 담당: Claude / 검증: Antigravity -->
<!-- 끝나면 [x]로 바꾸고 완료일을 붙여 "완료"로 옮긴다. 오래된 완료 항목은 지워도 된다(git 기록에 남는다). -->

## 진행 중



## 할 일

- [ ] (출시 후) ResultSequence의 3D 연출 4종(풍력·수력·발전소·연구소)을 공통 베이스(SetNeutral·ApplyAsync)로 묶기 — 직렬화 필드 타입이 바뀌어 4_Result 씬 재연결 필요
- [ ] (출시 후) CodingBlock(1300줄)의 컴파일 결과 하이라이트 부분을 별도 컴포넌트로 분리 — 블록 프리팹 재연결 필요
- [ ] (출시 후) TitleSceneManager의 QR 스캐너 입력을 별도 클래스로 분리하고 대기·확인·시작하기 상태를 HuliacDev.Core StateMachine으로
- [ ] (출시 후) 3_Game 드래그 레이어·코딩 패널 Content를 하위 Canvas로 분리(드래그·펄스 중 전체 재배칭 방지) — 프로파일러로 비용 확인 후, 하위 Canvas마다 GraphicRaycaster·셰이더 채널 필요
- [ ] 현장에서 아두이노 연동 확인 — 좌·우 PC의 명령이 차례로 실행되는지, 로그에 아두이노 응답(보낸 명령 미러링)이 찍히고 '다시 보냅니다' 경고가 없는지(응답이 막히면 같은 명령이 3번 실행됨), 첫 실행 때 Windows 방화벽 허용 창이 뜨는지
- [ ] 인트로 나레이션을 들으며 문구 줄이 문장마다 맞춰 올라오는지 확인 — 어긋나면 StreamingAssets/Json/1_Intro.json의 narrationLineStartTimes(나레이션 시작부터 각 줄이 올라오기 시작하는 초)를 고침
- [ ] 현장(Windows 빌드)에서 창 항상 위 확인 — 시작 프로그램으로 다른 프로그램과 함께 켜도 앱 창이 가려지지 않는지, 유지보수용 키보드로 Ctrl+F를 누르면 작업 관리자 같은 다른 창이 앞으로 오는지
- [ ] 레벨 선택 화면 V 표시 정식 그림 받으면 Assets/UI/2_Story/Icon_LevelComplete.png를 덮어써 교체(GUID 유지, 지금은 Claude가 만든 임시 그림)

## 완료

- [x] 블록 연결 받는 각도를 다시 180도(반원)로 — 반경 180px와 조건 블록 '더 가까운 자리'는 유지, 3_Game.json snapAngle 200 → 180 — PlayMode 252/252 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-28)
- [x] 값·조건(ValueOut·ConditionOut) 연결도 체인과 같은 200도·180px로 — 가로 판정을 오른쪽 반원에서 오른쪽을 가운데로 받는 각도로, 각도 설정 chainSnapAngle → 모든 연결이 함께 쓰는 snapAngle, 3_Game.json snapRadius 120 → 180, 리뷰 반영으로 조건 블록은 만약 헤더 자리·그리고 자리 중 더 가까운 쪽에 붙음 — PlayMode 252/252, Play 모드 레벨1 값 블록·레벨4 조건 블록으로 확인 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-28)
- [x] 레벨 선택 화면 수정 7건 — 모든 레벨 처음부터 해금, 결과 화면까지 간 레벨(서버 기록 포함)에 V(임시 그림), 인트로 홈·2_Story 홈과 < 버튼(레벨 선택 <는 인트로로, 홈은 move_idle·exit 후 타이틀), 시작하기 블록 고정, 체인 연결 200도·180px(3_Game.json 각도 설정 추가), 레벨5 '함수 만들기'·'함수 실행하기' 탭 분리, 두 함수 블록을 잇는 흐르는 점선, 레퍼런스 이미지는 드라이브 ProjectFiles/DG_AI_Zone1/references로, Player Settings Version 26.10.28 — PlayMode 244/244, Play 모드 화면 캡처로 확인 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-28)

- [x] 게임 화면 코딩 완료 뒤 비활성 버튼(코딩 완료·미션 다시보기·힌트·넘어가기)이 연해지지 않게 — 네 버튼의 Color Tint 비활성 색을 평소 색(흰색·알파 1)과 같게 — 담당: Claude / 검증: 서브에이전트 (2026-10-25)
- [x] 결과 화면 함수 정의 블록을 4존처럼 함수 사용 블록과 함께 올리기 — 정의 안쪽 블록 1초 간격 뒤 완성하기 맨 마지막, 쓰이지 않은 정의는 시작하기 묶음 뒤 — PlayMode 228/228, Play 모드로 레벨5 AI 결과 시각 측정 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-25)
- [x] 결과 화면 코딩 결과 칸 오른쪽 스크롤바 — 3_Game의 Scrollbar Vertical(Scrollbar_Track·Handle)을 복제, 칸 폭 723→693, 스크롤바 영역도 넘기지 않는 칸 판정에 포함, 스크롤바를 끄는 동안 자동 스크롤 멈춤 — PlayMode 228/228, Play 모드 화면 캡처로 위치 확인 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-25)
- [x] 결과 화면 터치 안내 뒤 코딩 결과 칸(나의·AI) 안 터치는 완료 화면으로 넘기지 않고 스크롤만 — 눌린 위치가 ProgramScroll 안이면 넘기기에서 뺌(ResultSequence.IsCompletePress, ResultProgramView.ContainsScreenPoint) — PlayMode 228/228, Play 모드 확인은 사용자가 에디터 사용 중이라 생략 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-25)
- [x] 결과 화면 AI 코딩 결과 칸 끌기 스크롤 안 됨 수정 — 시작 때 막아 둔 Result_AI 입력(blocksRaycasts)을 페이드인 뒤 SetGroupInteractable로 켬 — PlayMode 228/228, Play 모드 확인은 사용자가 에디터 사용 중이라 생략 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-25)
- [x] 결과 화면 값·조건 블록도 4존처럼 — 부모 블록이 올라온 바로 뒤 미끄러져 오고(쉬는 시간 없음), 다음 블록은 그것을 기다리지 않고 부모가 붙기 시작한 1초 뒤에 붙음(programBlockInterval을 붙은 뒤 쉬는 시간 → 블록 시작 간격 1.0으로, 씬 revealInterval도 1) — PlayMode 228/228, Play 모드로 레벨2·4 AI 결과 시각 측정 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-25)
- [x] 결과 화면 블록 붙는 속도를 4존과 같게 — 4_Result.json의 programBlockRiseDuration 0.4→0.7·programValueSlideDuration 0.25→0.4·programBlockInterval 0.15→0.3(0.7+0.3으로 4존 블록 간격 1초, 이후 시작 간격 1.0으로 바뀜), 코드 기본값도 같게, Player Settings Version 26.10.25 — 값·조건 블록이 붙은 블록은 1존이 따로 붙여 다음 블록이 0.7초 늦게 시작(구조 차이) — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-25)

- [x] 결과 화면 레이아웃 변경 — '항목 | 값' 대신 게임에서 조립한 블록(나의 코딩 결과)과 레벨별 정답 블록(AI 코딩 결과)을 게임 블록으로 다시 조립해 4존처럼 하나씩 붙이고(체인·안쪽은 아래에서 올라옴, 값·조건은 오른쪽에서 미끄러짐, ㄷ자 블록은 안쪽이 붙을 때 늘어남) 아래로 자동 스크롤(함수 정의는 오른쪽, 칸보다 넓으면 폭에만 맞춰 줄임), 제목 아래 문제 설명 상자, 새 패널 배경(Panel_CodingResultPlayer/AI.png), 레퍼런스 이미지는 드라이브 ProjectFiles/DG_AI_Zone1/references로, Player Settings Version 26.10.23 — PlayMode 228/228, Play 모드로 레벨1(건너뛰기)·4·5 결과 확인 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-23)

- [x] 인트로 튜토리얼 이미지 넘기기를 이미지 좌·우 터치에서 양옆 < > 버튼으로 변경 — TutorialPanel에 Button_Previous·Button_Next(160×160, 검정 알파 0.9, 글자 120, 가로 ±830), 첫 장에서 < 버튼 숨김, 안내 문구 '화면을 눌러서' 삭제, Player Settings Version 26.10.22 — PlayMode 227/227 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-22)

- [x] 튜토리얼 이미지 7번을 새 이미지(에피소드 건너뛰기 안내)로 교체 — 사용자가 넣은 '튜토리얼 7.png'의 내용으로 Tutorial7.png만 덮어써 GUID·Addressables 주소 Tutorial7 유지, 넣은 파일과 meta는 지움, Player Settings Version 26.10.20 — 콘솔 에러 0, PlayMode 미실행(코드 변경 없음) — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-20)

- [x] HuliacDev Template 26.10.16-1 → 26.10.19-1 반영(packages-lock 고정 커밋 a036657 → 7fbccfe — SoundManager.PreloadAsync 추가, 로드 중 StopSFX 뒤 재생 막음), 인트로가 나레이션 클립을 다 읽은 뒤 페이드인해 앱을 켠 뒤 첫 재생도 줄 연출과 같은 순간에 시작 — PlayMode 227/227, Play 모드로 타이틀→인트로 전환해 확인 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-19)

- [x] 1존 에피소드 나레이션(Zone_1_Episode_Narration.mp3)을 1_Intro 페이드인이 끝나면 틀고, 인트로 문구 줄이 나레이션 문장마다 맞춰 올라오게 함(1_Intro.json narrationLineStartTimes, 튜토리얼로 넘어가면 나레이션 멈춤) — PlayMode 227/227 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-19)

- [x] HuliacDev Template 26.10.12-1 → 26.10.16-1 반영(packages-lock 고정 커밋 1c5e127 → a036657 — WindowFocusRestorer alwaysOnTop 옵션), Settings.json alwaysOnTop true로 창 항상 위(Ctrl+F로 포커스 복구와 함께 끄고 켬) — PlayMode 225/225 — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-19)

- [x] 핑크색 명령 블록(Command) 탭 이름을 '움직이기'에서 '동작'으로 변경(Action도 '동작'이지만 쓰는 레벨이 없어 탭이 겹치지 않음), Player Settings Version 26.10.19 — 컴파일 에러 0, PlayMode 미실행(탭 이름 검사 테스트 없음) — 담당: Claude / 검증: Antigravity + 서브에이전트 (2026-10-19)

- [x] 레벨1 패널이 성공 방향에서 제자리로 돌아오지 않아 1_success_1·1_success_2 바로 뒤에 1_success_3을 보내 되돌림(정답이 남쪽이면 1_success_3 한 번만) — PlayMode 225/225 — 담당: Claude / 검증: Antigravity (2026-10-13)

- [x] 드래그 진단 로그('입력 모듈이 …을 끄던 손가락을 놓쳐') 삭제 — 현장 로그의 장치가 Mouse라 원인(Windows가 터치에 마우스 입력을 섞음) 확인, 드래그 정상 — PlayMode 224/224 — 담당: Claude / 검증: Antigravity (2026-10-13)

- [x] 레벨1(태양광) 결과 '양호'일 때 아두이노 명령에 문제의 정답 방향 번호 붙이기(동쪽 1_success_1·서쪽 1_success_2·남쪽 1_success_3, 다른 레벨과 fail·enter·exit는 그대로) — 아두이노 펌웨어도 새 명령을 받도록 수정 필요 — PlayMode 224/224 — 담당: Claude / 검증: Antigravity (2026-10-13)

- [x] 터치 모니터에서 손가락으로 끈 블록이 자꾸 원래 자리로 돌아가는 문제 수정(드래그 중 Windows가 섞어 보내는 마우스 입력·길게 누르기 오른쪽 클릭에 입력 모듈이 터치 포인터를 바꾸거나 지움 → 손가락을 직접 읽어 따라가고 뗀 자리에 놓음, 입력 모듈이 손가락을 놓치면 장치 이름 로그), 실제 입력 모듈 재현 테스트 2개, Player Settings Version 26.10.13 — PlayMode 218/218 — 담당: Claude / 검증: Claude(사용자 요청) (2026-10-13)

- [x] HuliacDev Template 26.10.10-2 → 26.10.12-1 반영(packages-lock 고정 커밋 03cbcdb → 1c5e127 — Ctrl+F 포커스 복구 토글 화면 표시, Settings.json 실패 시 숨은 종료 버튼 좌상단·투명, 응답 없는 창에 AttachThreadInput 안 함, JsonLoader 시작 시 취소 확인, GetTextWithRetryAsync 시도별 시간 초과), 바뀐 동작과 어긋난 주석 정리(호출부 취소 확인 4곳은 읽기 오류 경로 때문에 유지) — PlayMode 216/216 — 담당: Claude / 검증: Antigravity (2026-10-12)

- [x] 게임 씬 블록이 끌자마자 원래 자리로 돌아가는 문제 수정(마우스·터치 모두, 10-10 끝 신호 감지가 입력 모듈이 매 프레임 덮어쓰는 pointerDrag를 봄 → device·pointerId로 판단), 실제 입력 모듈 회귀 테스트 3개 — PlayMode 216/216 — 담당: Claude / 검증: Antigravity (2026-10-12)

- [x] 레벨1(태양광) 블록 목록에서 '북쪽' 값 블록 삭제(문제 값 후보·힌트에는 원래 북쪽 없음), Player Settings Version 26.10.12 — PlayMode 213/213 — 담당: Claude / 검증: Antigravity (2026-10-12)

- [x] 조명·장치 아두이노 UDP 연동 — 좌·우 PC 공용 아두이노(Arduino.json, 192.168.0.13:9000)에 n_enter(레벨을 골라 스토리)·n_success/n_fail(결과 입장, 양호만 성공)·n_exit(결과 다음 버튼·스토리 < 버튼·무입력 타임아웃·앱 종료 때 한 번) 전송, 아두이노 응답(보낸 명령 미러링)이 3초 안에 없으면 최대 3번까지 다시 보냄(명령은 차례로 하나씩, 뒤 명령이 기다리면 재전송 중단), PR 리뷰 13건 반영(2건은 아두이노·현장 처리 필요), Player Settings Version 26.10.11 — PlayMode 213/213 — 담당: Claude / 검증: Antigravity (2026-10-11)

- [x] 7차 점검 지적 수정 — 씬 전환(페이드) 중 터치를 연출 스킵·진행으로 받지 않음(SceneFader.IsLoading). 두 사람 드래그 핀치 판정(정상 플레이는 한 사람)·Alt+Enter(타깃 PC 키보드 없음)·관리자 레벨 이동 판 복귀(비활동 타이머로 충분)는 그대로 — PlayMode 188/188 — 담당: Claude / 검증: Antigravity (2026-10-10)
- [x] 7차 전체 점검(템플릿 26.10.10-2 연동·빌드 배포 설정·최근 수정 회귀·전체 흐름) — 회귀 없음, 중간 이상 확정 코드 버그 0건, 확인 필요 중간 1건(두 사람 드래그가 핀치로 판정)·낮음 2건(페이드 중 터치로 연출 스킵·아웃트로 종료 연타)·설정 낮음 4건, 목록은 docs/agents/HANDOFF.md — 담당: Claude / 검증: Antigravity + Claude 서브에이전트 (2026-10-10)
- [x] HuliacDev Template 26.10.10-1 → 26.10.10-2 반영(packages-lock 고정 커밋 36ec69a → 03cbcdb — JsonLoader 취소 예외·저장 결과 반환·임시 파일 저장, Settings.json 읽기 실패 시 대체 설정), 관리자 비밀번호 저장은 SaveAsync 결과로 판정, 바뀐 동작과 어긋난 주석 정리 — PlayMode 186/186 — 담당: Claude / 검증: Antigravity (2026-10-10)
- [x] HuliacDev Template 26.10.9-1 → 26.10.10-1 반영(창 포커스 복구 WindowFocusRestorer·Reporter 문자열 캐시 수정, packages-lock 고정 커밋 640d05e → 36ec69a), 템플릿 단축키 D·I·M·F를 ConfigureInputBindings override로 Ctrl 조합(F 추가), Settings.json focusRestoreDelay·focusRestoreRetryInterval 3초 — 담당: Claude / 검증: Antigravity (2026-10-10)
- [x] 6차 검사 지적 수정 — 관리자 창이 열린 동안 QR 무시·늦은 확인 결과 버림, 사라진 손가락 일괄 복귀 재시도, 복귀 순서(나중에 집은 블록부터) — PlayMode 185/185, 포커스 복구는 Template로 — 담당: Claude / 검증: Antigravity (2026-10-10)
- [x] 6차 전체 검사(5차 수정 회귀·빌드 환경 차이·비동기 경합) — 중간 이상 코드 버그 0건, 낮음 4건, 설정·운영 결정 5건, 목록은 docs/agents/HANDOFF.md — 담당: Claude / 검증: Antigravity + Claude 서브에이전트 (2026-10-10)
- [x] 5차 검사 지적 수정 — 끝 신호 없이 사라진 손가락의 드래그 복귀, 핀치 복귀 순서 재시도·행동 로그, 레벨5 스토리 오타(계선→계산), 템플릿 Reporter는 별도 프롬프트로 — PlayMode 183/183 — 담당: Claude / 검증: Antigravity (2026-10-10)
- [x] 5차 전체 검사(수정 회귀·장시간 운영·데이터 정합성) — 4차 수정 회귀 없음, 중간 2건(OnEndDrag 없이 포인터가 지워지면 블록이 떠 있음, 템플릿 Reporter 문자열 캐시 증가)·낮음 2건·레벨5 스토리 오타 확인, 목록은 docs/agents/HANDOFF.md — 담당: Claude / 검증: Antigravity + Claude 서브에이전트 (2026-10-10)
- [x] 4차 검사 지적 전부 수정 — 같은 블록 두 손가락(먼저 끈 손가락만, 자기 순환 방지), 핀치 복귀 재검사, 결과 타이머 재개·관리자 레벨 이동 체험자·결과 판정·튜토리얼 이미지 — PlayMode 181/181, 멀티터치는 현장 확인 필요 — 담당: Claude / 검증: Antigravity (2026-10-10)
- [x] 4차(마지막) 전체 검사 — 높음 1건(두 손가락이 같은 블록을 동시에 잡으면 소켓 상태가 꼬이고 최악에 무한 루프), 중간 1건(핀치 복귀가 찬 소켓 덮어씀), 낮음 4건·확인 1건, 목록은 docs/agents/HANDOFF.md — 담당: Claude / 검증: Antigravity + Claude 서브에이전트 (2026-10-10)
- [x] 3차 감사 지적 전부 수정 — 두 손가락 동시 드롭(놓을 때 소켓 재검사), 낮음 6건, 레벨4 실행되지 않는 만약의 조건 0점(B안), 정리 5건, 여러 문장 summary 166곳 한 문장으로 — PlayMode 177/177, 터치 2건은 현장 확인 필요 — 담당: Claude / 검증: Antigravity (2026-10-10)
- [x] 3차 전체 코드 감사(스킬 위반·버그·성능·리팩터링) — 확정 중간 1건(두 손가락 동시 드롭으로 값·조건 소켓 덮어쓰기)·낮음 6건·기획 확인 1건·여러 문장 summary 98개, 목록은 docs/agents/HANDOFF.md, 수정 범위는 사용자 결정 — 담당: Claude / 검증: Antigravity + Claude(시간 초과 묶음 대신 확인) (2026-10-10)
- [x] 2차 전체 감사와 수정 — 수력 물 셰이더(위상 튐·장시간 정지), 함수 정의·시작하기 체인 스냅, '완성하기' 안쪽 진입, 탭 다른 블록 행동 로그, 함수 정의 안 호출 검사, 스캐너 입력 버튼 눌림(Send Navigation Events 끔), 드래그 3건(강조 연결부에 붙기·묶음 갈라짐·두 손가락, Play 모드 재현 후), 낮음 방어·정리 — PlayMode 173/173 — 담당: Claude / 검증: Claude(Antigravity 사용 한도로 대신 검증) (2026-10-09)
- [x] 빌드 후 바꿀 값 JSON으로: 로봇 영상 경로(00_Common.json), 관리자 창 자동 닫기 시간·진입 클릭 수(Admin.json, 비밀번호 저장 때 다른 값 유지), 타이틀 안내 문구 7개(0_Title.json), 안 쓰는 영상 2개 삭제 — PlayMode 163/163 — 담당: Claude / 검증: Claude(Antigravity 사용 한도로 대신 검증) (2026-10-09)
- [x] 로그 정리: 운영 진단(서버 응답·관리자 조작·QR 오입력·점수 근거)과 체험자 행동만 남기고 자잘한 정보 로그(실행 재생·씬 전환 대기·QR 길이) 삭제, 체험자 행동(화면 이동·코딩 조작·블록 붙이기·떼기)은 '{name}이 ~를 함 — 결과.' 형식(조사 이/가 자동, 4존 T53과 같게) — PlayMode 156/156 — 담당: Claude / 검증: Antigravity 2묶음 + Claude(agy 한도로 대신 검증) (2026-10-09)
- [x] 출시 전 감사 결과 수정 — 버그 6건(조건 3개 이상, 체인 끼우기 순서, 성공 연출 중 넘어가기, 스토리 줄 사이 스킵, 관리자·이름 창 60초 자동 닫기, 연결 안 된 만약 안 아니면 안내)·방어 코드·스킬 규칙·성능·미사용 코드 정리, Space 디버그 키는 개발 빌드만, 리뷰에서 나온 ㄷ자 블록 헤더 드래그 회귀 수정 — PlayMode 155/155 — 담당: Claude / 검증: Antigravity (2026-10-09)
- [x] 출시 전 전체 코드 감사(스킬 위반·성능·정리) — 확정 버그 6건·방어 부족 5건·규칙·정리 목록은 docs/agents/HANDOFF.md, 수정 범위는 사용자 결정 — 담당: Claude / 검증: Antigravity(타이틀은 Claude가 대신) (2026-10-09)
- [x] HuliacDev Template 패키지 26.9.25-3 → 26.10.9-1 업데이트(packages-lock.json 고정 커밋 b4547f3 → 640d05e, VideoManager 영상 RenderTexture 깊이 버퍼 제거 — 이 프로젝트는 해당 API를 쓰지 않음), Player Settings Version 26.10.9 — 담당: Claude / 검증: Claude(Antigravity 한도 초과로 대신 검증) (2026-10-09)
- [x] 타이틀 대기 중에는 move_idle_timeout을 보내지 않고(APIManager가 0_Title 타임아웃 무시), QR 확인 뒤 시작하기 대기 시간이 지나 QR 대기로 돌아갈 때만 TitleSceneManager가 한 번 보냄 — PlayMode 120/120 — 담당: Claude / 검증: Antigravity (2026-10-08)
- [x] 체험자 서버 JSON 응답 앞뒤 군더더기 무시(현장 getUser가 JSON 끝 } 뒤에 ``` 줄을 붙여 보내 모든 체험자가 타이틀에서 막힘) — getUser·updateValue는 첫 { ~ 마지막 }만 읽고, getUser 실패 사유에 JsonUtility 오류 문구 포함, Player Settings Version 26.10.8 — PlayMode 120/120 — 담당: Claude / 검증: Antigravity (2026-10-08)
- [x] 관리자 화면에서 레벨 이동·모드 변경으로 씬을 떠나는 중 닫기·레벨 버튼 입력 무시(4존 _isLeaving) — PlayMode 114/114 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 체험자 서버 API 방어(4존 맞춤) — getUser는 A1~A5만 보기(A0·A6·A10 무시), Server.json 로드 뒤 취소 전달, VisitorSettings 로드 실패 시 에러 후 기본 인스턴스 — PlayMode 114/114 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 타이틀 QR 입력 글자 간격 초기화(4존 T45) — 글자 사이가 0_Title.json scanCharGapSeconds(0.5초)보다 벌어지면 앞 글자를 버리고, 늦은 Enter는 QR로 보지 않음, 0_Title.json은 qrCanvasGroup이 없어도 항상 읽기 — PlayMode 114/114 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 레벨 결과 업로드 로그에 체험자 이름 추가(idx·이름·코드=값, uid는 남기지 않음) — PlayMode 106/106 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 서버 모드 타이틀: QR 확인 뒤 '○○님, 시작하기를 눌러주세요' 문구, 시작하기가 떠 있어도 새 QR을 받아 그 사람으로 다시 확인, 시작하기를 안 누르면 비활동 타이머 값(Settings.json useInactivityTimer·resetTime) 뒤 QR 대기로 — PlayMode 106/106 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 타이틀 'QR 코드를 확인하고 있습니다'를 최소 1초 보여 주기(서버가 빨리 답해도 깜빡이지 않게, 0_Title.json qrCheckingMinSeconds) — PlayMode 106/106 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 체험자 서버 API 요청 실패 시 재시도(연결 실패·시간 초과·HTTP 오류만, 서버가 답한 결과는 재시도 안 함) — Server.json의 upload…(결과 업로드 10회·5초)·qrCheck…(타이틀 QR 확인 3회·3초)·retryDelaySeconds(1초) — PlayMode 106/106 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 진행도 API 연동(getUser) — QR 확인 후 A1~A5 중 기록 있는 마지막 레벨의 다음까지 해금, getUser 실패는 안내 후 QR 대기, 서버 모드도 타이틀 복귀 때 진행도 초기화 — PlayMode 106/106 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 레벨 결과 서버 업로드(updateValue) — 1존 코드 A, 레벨1~5 = A1~A5, 미션 성공 1·실패 0(넘어가기 포함), 서버 모드·QR 확인 체험자만, 관리자 레벨 이동 판 제외, 응답 result로 저장 판정 — PlayMode 95/95 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 서버 모드 QR 체험자 확인(checkActive) — 체험 가능하면 idx·이름 기록 후 시작하기, 완료·없음·서버 오류는 안내 후 다시 QR 대기, 서버 주소는 StreamingAssets/Json/Server.json — PlayMode 78/78 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 템플릿 디버그 단축키 D·I·M을 Ctrl+D·Ctrl+I·Ctrl+M으로(QR 스캐너 uid 문자와 충돌 방지, 템플릿은 그대로 두고 런타임 바인딩 오버라이드) — PlayMode 66/66 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 블록 고르기의 '변수' 탭(레벨1·2 값 블록) 이름을 레벨3처럼 '숫자·정보'로 — PlayMode 63/63 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 영상 오디오 트랙 제거 — Robot_260728.webm의 Opus 트랙을 ffmpeg 스트림 복사(-map 0:v -c copy -an)로 제거, 알파 포함 프레임 동일 확인, 인트로 재생 시 OPUS 에러 사라짐 — 담당: Claude (2026-10-07)

- [x] 관리자 페이지 기능: 비밀번호 변경(키패드 두 번 입력 → Admin.json), 로컬/서버 모드·체험자 이름 변경(Visitor.json → VisitorSettings SO + PlayerPrefs, 이름은 GCON_3 화면 한글 키보드 + 숫자열), 선택한 레벨의 스토리 화면으로 바로 이동, 스토리 좌상단 < 버튼(정상 진입→레벨 선택, 관리자 레벨 이동→타이틀 관리자 화면), 관리자 판은 결과 뒤 타이틀 관리자 화면으로 — PlayMode 63/63 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 관리자 페이지 진입: 타이틀 좌상단 3초 안에 10회 터치 → 비밀번호 키패드(789/456/123/확인0←, Admin.json 기본 0000·4~6자리, 닫기·10초 무입력 시 닫힘) → 관리자 화면(제목 '관리자 페이지'+닫기, 기존 UI 이미지 없이 단색 러프 UI), 투명 터치 영역은 RaycastArea — PlayMode 51/51 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] 게임 씬 코딩 존을 조금 축소된 배율(0.8)로 시작 — CodingZoneZoom.startZoom, 블록 스폰 전 Awake에서 적용 — PlayMode 45/45 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] ㄷ자 블록(반복하기·만약·함수 정의) 하단 막대를 잡아도 드래그되게 — 프리팹 Footer에 투명 터치 영역(FooterDragArea), 회귀 테스트 CBlockDragAreaTests, Player Settings Version 26.10.7 — PlayMode 45/45 — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] MCP for Unity 패키지 10.2.0 → 10.3.0 업데이트(packages-lock.json 고정 커밋 갱신), Player Settings Version 26.10.4 — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] 디버그 키 정리: Space를 입력 액션(GameInputActions.Debug.Shortcut)으로 — 레벨 선택 화면 모든 레벨 해금·게임 화면 컴파일 검증, TestScene 1~7·R 테스트 키(SolarPanelModelPoseTestInput) 삭제 — PlayMode 41/41 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 결과 씬 레벨5 연구소 스테이지(Prefab_Lab, 건물 안 노란 조명 — 부족 꺼짐·보통 약하게 깜빡임·양호 강하게), 양호 기준 75%, 스테이지 카메라 10개를 모델이 잘리지 않는 최대 크기로 조정(발전소 피스톤 이동 포함) — PlayMode 40/40 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 레벨5 컴파일 규칙: 메인 체인 함수 전용 제한·함수 호출 필수·빈 함수 정의 에러 제거, 함수 호출 시 함수 정의 블록이 코딩 영역에 없으면 에러 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 레벨5 채점(함수 안 에너지 수, 4개 100%)·결과 행(함수 사용·태양광·풍력·수력 발전·스마트 도시 발전소 ON/OFF·전력 수급 상태), 블록 '풍차' → '풍력' — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 레벨5 블록 레이아웃: '미래 에너지 만들기' 함수 정의·호출 + 에너지 블록 4개(태양광·풍차·수력 발전·스마트 도시 발전소) — PlayMode 33/33 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 레벨4 힌트 문구 추가('만약에 밤 그리고 과부하 이면 / 놀이 시설은 끄고, 병원은 계속 전기 켜기'), 그림을 레벨3 힌트와 같은 크기로 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 반복하기 블록 라벨 '반복하기(무한)'(레벨 2·4·5), 블록 종류 판별을 키워드 포함으로, 디버그 코드 출력 중복 방지 — PlayMode 33/33 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 레벨4 놀이시설 항목: 행 이름 '놀이시설 끄기 조건(만약)', 무한 반복 뒤라 실행되지 않는 놀이시설 불 끄기 불인정 — PlayMode 33/33 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 레벨4 함정 조건 블록(낮·전기 여유) 추가, 조건에 함정이 들어가면 조건 0점 — PlayMode 32/32 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 레벨4 채점: 반복하기 위치와 상관없이 만점(놀이시설 불 끄기가 만약 안·병원 불 켜기가 반복 안이면 만점), 함정 동작 블록(놀이시설 불 켜기·병원 불 끄기) 추가, 결과 '반복 감지' → '놀이시설 전력 차단' — PlayMode 31/31 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 빈 아니면(아니면 아래 블록 없음) 컴파일 에러, 아니면 블록 빨간 점멸 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 레벨3 결과에 '수문 열기·닫기 순서'(정상/오류) 행 추가, '감지된 강물의 높이' 행 제거 — 순서 채점과 같은 기준, PlayMode 28/28 통과 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 컴파일 외곽선·스냅 하이라이트를 셰이더로 블록 모양 둘레에 일정 두께로 그리기(9-slice 정확 대응, 체인=아래 가장자리·값=오른쪽 옆면·ㄷ자 안쪽=머리 아래 가장자리) — 리뷰·PlayMode 27/27 통과 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 시작하기→완성하기만 이으면(사이에 블록 없음) 컴파일 에러, 두 블록 빨간 점멸 — 리뷰·PlayMode 25/25 통과 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 움직이기(Command) 블록은 사용하지 않아도 컴파일 통과(미사용 블록 에러에서 제외), 리뷰·PlayMode 25/25 통과 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 레벨5(미래에너지) 문제 문구 교체 — 임시 태양광 문제를 기획 문구로 바꾸고 LevelQuestionDataTests의 레벨5 제외 삭제, 리뷰·PlayMode 23/23 통과 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 효과음 8종 연결(블록 장착, 버튼 클릭(인트로·튜토리얼 터치 포함), 코딩 경고·완료, 게임 시작, 힌트·미션 다시보기, 미션 성공·실패) — 리뷰·PlayMode 23/23 통과, Play 모드 청취 확인은 남음 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 감사 후속 2차(ZLogger 전환, SerializeField 연결, Raycast Target, FlowInnerResize·Fitter 충돌 수정, 스냅 선정 통합) — PR #63, 리뷰·PlayMode 22/22 통과 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] (기획 확인) 미션 실패여도 다음 레벨이 열리는 동작 — 의도대로 유지, ResultSequence.OnNextClicked에 주석 (2026-10-02)
- [x] 문제 문구·후보 값·정답·힌트 그림을 Constants.Questions·HintPanel에서 LevelData(SO)로 옮기기 — PR #62, 리뷰·PlayMode 22/22 통과 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 스킬 준수 감사 1차 반영(타이머 버그와 회귀 테스트, 미사용 코드, 제어자 순서, 드래그 핫패스, LevelKind) — PR #61, 리뷰·PlayMode 19/19 통과 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 프로젝트 설정 정리: Player Settings Version을 수정일 26.10.1로 — 담당: Claude / 검증: Antigravity (2026-10-01)
- [x] 씬별 카메라 후처리 정리: UI 전용 씬(0_Title·1_Intro·2_Story·5_Outro) 후처리 끔, 4_Result의 3D 렌더텍스처 카메라 8개는 투명 배경 유지를 위해 끈 채 유지 — 담당: Claude / 검증: Antigravity (2026-10-01)
- [x] 게임 씬 도움말 버튼 문구를 '힌트'로 복원 — 담당: Claude (2026-09-29)
- [x] 레벨1~4 storyText 오타·\r 정리 — 담당: Claude (2026-09-29)
- [x] 수력 결과 '수문 닫기 조건(아니면)' ON/OFF (기획 A-9) — 담당: Claude (2026-09-29)
- [x] 수력 힌트 규칙 문구 추가 (기획 A-8) — 담당: Claude (2026-09-29)
- [x] 랜덤 문제 값 [ ] 표시 (기획 A-6) — 담당: Claude (2026-09-29)
- [x] 인트로 시작 버튼 제거·화면 터치 전환 (기획 A-4) — 담당: Claude (2026-09-29)