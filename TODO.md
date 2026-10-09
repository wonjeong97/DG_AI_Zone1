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

## 완료

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