# HANDOFF

에이전트 간 인계 기록. 새 항목을 맨 위에 추가한다.

## 항목 형식

```
### [YYYY-MM-DD HH:MM] <보낸 쪽> → <받는 쪽> · <TASKS ID>
- 변경 파일:
- 확인 요청:
- 결과: (받는 쪽이 작성 — 통과/실패와 근거)
```

---

### [2026-10-02 10:16] Claude · T7 감사 반영 Unity 검증 (refactor/skill-audit-fixes, main 병합 후)
- 변경 파일: 없음(검증만). main 병합(8d3fb8a) 뒤 Zone1 에디터에서 확인.
- 확인 요청: 컴파일·콘솔 에러, PlayMode·EditMode 테스트
- 결과: 통과. Claude가 직접 실행 — agy 실행 파일이 이 PC에서 없어져(`AppData\Local\agy\bin` 없음) 대신 실행함. 컴파일 에러 0, PlayMode 16/16 통과, EditMode 테스트 0개, 콘솔은 MCP WebSocket 경고 1건뿐.
  - MCP run_tests(PlayMode)가 실행 중에 EditorSettings.asset을 `m_EnterPlayModeOptionsEnabled: 1`로 저장하고 메모리에서만 되돌림 → 메모리 값 False 확인 후 파일을 git checkout으로 되돌림.
  - 블록 드래그 스냅과 레벨별 결과 화면은 Play 모드에서 직접 조작해 보지 않음(테스트는 컴파일·채점·소켓 점유만 다룸).

### [2026-10-01 23:00] Claude → Antigravity · 프로젝트 설정 정리
- 변경 파일: ProjectSettings/ProjectSettings.asset
- 확인 요청: bundleVersion 이 26.10.1 인지, m_EnterPlayModeOptionsEnabled 가 0 인지
- 결과: 통과(파일 기준 검증).

### [2026-10-01 22:40] Claude → Antigravity · 씬별 카메라 후처리 정리
- 변경 파일: Assets/Scenes/0_Title.unity, 1_Intro.unity, 2_Story.unity, 5_Outro.unity (Main Camera Post Processing 끔)
- 확인 요청: 모든 씬·프리팹에 m_RenderPostProcessing: 1 이 남았는지, 4_Result 렌더텍스처 카메라 8개의 Clear Flags·배경 알파, 이 카메라들의 후처리를 끈 채 두는 것이 맞는지
- 결과: 통과. 후처리 켜진 카메라 0개. 4_Result 카메라 8개는 SolidColor·알파 0으로 3D 모델을 UI 위에 합성하며, URP 14 후처리는 알파를 1로 덮어써 검은 박스가 생기고 프로젝트에 Volume도 없으므로 끈 채 유지가 맞다는 의견.
### [2026-10-01] Claude → Antigravity · T7 감사 결과 반영 (refactor/skill-audit-fixes)
- 변경 파일: ResultSequence.cs(일반 예외 시 비활동 타이머 재개), CodingBlock.cs·BlockSocket.cs·BlockFactory.cs·CategoryButtonUI.cs·CodingZone.cs·Constants.cs(미사용 코드 삭제), CodingSlot.cs(삭제), static readonly 13곳 순서, CodingZoneZoom.cs(휠 0 비교), CodingBlock.cs(드래그 스냅 진입 소켓 캐싱·IsRepeat), BlockCompiler.cs(IsRepeat 사용), LevelData.cs(LevelKind)·01~05_*Data.asset(kind)·BlockScorer.cs·GameSceneManager.cs·ResultSequence.cs·BlockScorerTests.cs(레벨 판별을 kind로), CHANGELOG.md
- 확인 요청: 단계별 diff 리뷰(삭제 심볼 잔여 참조, 예외 분기 순서, 진입 소켓 캐시가 쓰이기 전 채워지는지, IsRepeat 동치, LevelKind와 기존 이름 판별 동치·레벨5 경로·null 기본값, LevelData 에셋 누락)
- 결과: 통과(2회 호출, 8개 항목 모두 통과). Claude가 Unity 생성 csproj를 dotnet build로 컴파일 확인 — DG.Zone1·Tests·Editor 오류 0, C# 경고 0. Zone1 에디터가 MCP에 연결되어 있지 않아(MCP 서버에는 Zone4만 연결) PlayMode 테스트·Play 모드 확인은 하지 못함.

### [2026-10-01] Claude → Antigravity · T7 프로젝트 전체 스킬 준수·최적화 감사
- 변경 파일: 없음(읽기 전용 감사). agy 6건 병렬(CodingBlock / BlockFactory·Spawner / 존·소켓 / 컴파일러·채점 / 결과·게임 씬 / 타이틀·스토리 씬), Claude는 grep 기반 규칙 점검과 App·Data·Network·Constants·씬 레이캐스트 직접 검토.
- 결과: agy 지적을 코드로 확인해 확정·오탐을 나눔.
  - 확정(규칙): `static readonly` 순서 13곳, GetComponentInChildren/InParent 2곳(IntroSceneManager:60, CodingZoneZoom:54), 로거 미주입 컴포넌트의 Debug.Log 12곳, `Mathf.Approximately(wheel, 0f)`(CodingZoneZoom:80), GameSession SO를 런타임 상태 저장소로 사용, 표시 전용 TMP 텍스트 Raycast Target 켜짐(3_Game 약 22개 등), ResultRowsView가 IObjectResolver 대신 Instantiate.
  - 확정(최적화): 드래그 중 매 이벤트 `transform.Find`+TryGetComponent·`name.Contains`(CodingBlock 스냅 탐색), FlowInnerResize.LateUpdate 매 프레임 체인 순회·형제 TryGetComponent.
  - 확정(정리): 죽은 코드 CodingBlock.ReturnHome·CodingSlot·BlockFactory.CreateEmptyCodingSlot·CategoryButtonUI.SetReferences, 스냅 대상 선정 로직 중복(UpdateSnapHighlight/TrySnapToSocket), 레벨 종류를 에셋 이름으로 판별(Constants.Levels.IsXxx 22곳).
  - 확정(버그): ResultSequence.PlaySequence에서 취소 외 예외 시 비활동 타이머가 멈춘 채 남음(이전 리뷰에서 보류한 건).
  - 잠재(현재 데이터로 재현 불가): 함수 정의 안 함수 블록 시 무한 재귀(레벨5 함수 블록 1개라 불가), 만약 안 아니면 2개 통과(레벨3 아니면 1개).
  - 오탐: 씬 매니저의 GameManagerBase 상속 요구(GameManagerBase는 DontDestroyOnLoad 싱글톤), ReturnHome InnerSocket 누락(호출처 없음), SnapInto Kill 후 좌표 튐(1135줄 가드), 드롭 시 parent null(드래그 중 부모는 캔버스), typeof(RectTransform)로 만든 오브젝트의 TryGetComponent 반환값 미검사.
  - 결정 필요: 미션 실패여도 다음 레벨 해금(ResultSequence.OnNextClicked) — 기획 확인.
  - 스킬 문서 보완 필요: 1번 "씬/전역 매니저는 GameManagerBase 상속" 문구가 싱글톤과 충돌, 23번 예시가 `private static readonly` 순서.

### [2026-10-01] Claude → Antigravity · 블록 탭 이름 변경·탭 라벨 겹침 (#60)
- 변경 파일: Constants.cs(Condition "숫자·정보", Command "움직이기"), CategoryZone.cs(셀 180×52, 열 간격 32), CategoryButton.prefab(라벨 폭 128·왼쪽 정렬·줄바꿈 끔), GamtanRoadTantan SDF.asset(Dynamic 아틀라스 글자 추가), 1_Intro.unity(직업 이름 "신재생에너지 전문가", 사용자 편집), CHANGELOG.md, REVIEW_ITEMS.md
- 결과: 통과(콘솔 에러 0, validate_script 이상 없음, 버튼 내부 합계 180 = 셀 180, 2열 총폭 408 ≤ 컨테이너 494, 인트로 {name}·color 태그 정상, CHANGELOG·REVIEW_ITEMS 반영 확인). 첫 호출은 확인 범위가 넓어 5분 제한에 걸려 둘로 나눠 다시 실행.
  - 추가 지적 2건 보류: 프리팹 루트 sizeDelta(138×42)와 셀 크기 불일치는 이전부터 있었고 런타임에 그리드가 강제함, 그리드 정렬 여백은 총폭을 408로 유지해 이전과 동일.
  - Claude가 Play 모드(3_Game)에서 확인: 탭 4개 모두 ColorBox와 글자 사이 8px, 한 줄, 버튼 안에 들어옴(숫자·정보는 오른쪽 끝까지 9px 여유). 옛 탭 이름('제어'/'동작')을 쓰는 씬·프리팹·에셋·JSON 없음(ripgrep).

### [2026-09-30] Claude → Antigravity · 결과 씬 새 디자인(피그마 에피소드1-6)
- 변경 파일: ResultRowsView.cs·ResultRowView.cs(신규), ResultSequence.cs, Constants.cs(행 이름 상수, 결과 포맷 문자열 제거), ResultSceneSettings.cs·4_Result.json(rowFadeDuration, touchGuideDelay, 속도 조정), TypewriterTextTMP.cs(SetText 방어), 4_Result.unity(패널 재구성·확인 버튼→터치 안내), UI 리소스(Row_Result·Badge_Efficiency·Line_ResultDivider 추가, Panel_ResultPlayer/AI 교체), docs/design/Result_Episode1-6.png(시안)
- 결과: agy 리뷰 4건 중 3건 반영 — 발전소 스킵 시 병원 전력 유지 '-' 복원, 행 컴포넌트 지연 조회·TypewriterTextTMP.SetText 방어, 행 높이 계산에 padding 반영·음수 방지. 취소 외 예외 시 타이머 재개는 기존 구조라 보류.
  - Claude가 Play 모드에서 확인: 태양광 3행·수력 5행 배치, % 숫자와 배지 겹침 해소, 부족 시 흑백, 터치 시 완료 패널(미션 실패!) 전환, 효율 카운트 약 1.5초, AI 100% 후 약 1.1초 뒤 터치 안내.
  - 값 '-'가 위로 치우치는 문제: 행 텍스트 세로 정렬이 Midline(Geometry, 보이는 글자 기준)이라 타이프라이터가 글자를 숨긴 채(maxVisibleCharacters=0) 계산한 위치가 남았음 → Middle(줄 높이 기준)로 변경해 해결.

### [2026-09-30] Claude → Antigravity · 결과 씬 미션 성공/실패
- 사전 조사(agy): 4_Result UI 계층·연출 순서·판정 값 위치. '미션 완료!'는 CompletePanel/Back/Text (TMP). 결과 텍스트 박스(604x300)와 3D 모델 이미지(321x214)가 226x134px 겹침 — 결과 창 디자인 개선 때 반영 필요.
- 변경 파일: ResultSequence.cs(completeTitleText, 성공 판정), Constants.cs(MissionSuccess/MissionFail), 4_Result.unity(참조 연결)
- 결과: 리뷰 문제 없음. Claude가 Play 모드에서 확인 — 100%·50% 성공, 40%·건너뜀 실패.

### [2026-09-30] Claude → Antigravity · 핀치 중 블록 드래그 방지
- 변경 파일: CodingBlock.cs(IsDragCancelled, CancelActiveDrags/RestoreDragHome), CodingZoneZoom.cs(IsMultiTouch, 핀치 시 드래그 취소), CodingZone.cs·BlockZone.cs(취소된 드래그 드롭 무시)
- 결과: PlayMode 16/16 통과, 콘솔 에러 0. 지적 반영 2건 — 핀치 중 한 손가락을 뗐다가 블록을 집고 다시 대면 취소가 누락되는 경로(두 손가락이 닿아 있는 동안 매 프레임 취소로 수정), OnDisable에서 IsDragCancelled 미초기화. 한 손가락은 코딩 판·한 손가락은 인벤토리인 경우 기존 드래그 유지는 핀치가 아니므로 의도대로 둠.
  - Claude가 Play 모드에서 확인: 시작→반복→완성 체인에서 반복을 집은 뒤 핀치 취소 시 체인 원래대로 복원, 이어지는 OnDrop/OnEndDrag가 블록을 옮기지 않음, 가상 터치 2개가 닿은 상태에서는 블록을 집지 못함.

### [2026-09-30] Claude → Antigravity · 코딩 존 확대/축소, 카테고리 이름 맞바꿈
- 변경 파일: CodingZoneZoom.cs(신규), CodingBlock.cs, FlowInnerResize.cs, Constants.cs, 3_Game.unity(스크롤바 삭제·scrollSensitivity 0·CodingZoneZoom 부착), Board_CategorySelect.png 삭제
- 사전 조사(agy): 코딩 존 구조와 Content 배율 적용 시 틀어지는 지점 10곳 — 부모 이동 4곳은 OnTransformParentChanged로, 스냅 반경 4곳은 배율 곱으로, 완성하기 초기 위치·FlowInnerResize 높이 계산은 배율 반영으로 수정.
- 결과: PlayMode 16/16 통과, 콘솔 에러 0. 핀치 중 손가락 하나를 뗐다가 다시 대면 배율이 튀는 문제 지적 → 반영. 핀치 첫 손가락이 블록 위면 블록 드래그가 함께 시작되는 점은 알려진 제한으로 남김. 카테고리 이름 지적은 사용자 결정 사항이라 해당 없음.
  - Claude가 Play 모드에서 확인: 휠 확대 시 커서 아래 지점 고정, 최소 배율(0.514)에서 판이 뷰포트를 채움, 가상 터치 2개를 두 배로 벌려 0.51→1.03 확대 후 이동 복원, 배율 0.51에서도 반복 블록 내부 높이 동일(81/323), 인벤토리 블록이 코딩 존 배율을 따름.
### [2026-09-29] Claude → Antigravity · 타이틀 하단 안내·QR 흐름
- 변경 파일: TitleSceneManager.cs, Constants.cs(TitleMessages), 0_Title.unity(guideText 연결)
- 확인 요청: 코드 리뷰(구독 누수, 비동기 취소, Enter 판정, null 처리)
- 결과: 지적 7건 중 1건 반영. 스캐너가 별도 키보드 장치일 때 Keyboard.current 캐싱으로 입력을 놓치는 문제 → 연결된 모든 키보드와 이후 연결 장치를 구독하고 CR/LF 문자로도 인식 완료하도록 수정. Keyboard.onTextInput 부재 지적은 오탐(컴파일·동작 확인), 일반 예외·null 지적은 JsonLoader가 예외를 삼키고 new T()를 반환해 해당 없음, 스타일 지적 2건은 보류.
  - Claude가 Play 모드에서 확인: 서버 미연동 시 시작하기 안내·버튼 표시, 서버 연동 시 QR 안내·버튼 숨김 → 나중에 추가한 가상 스캐너 장치의 'VISITOR-5678\r' 입력으로 인식 완료·버튼 표시·구독 해제.

### [2026-09-29] Claude → Antigravity · T1~T5
- 변경 파일: IntroSceneManager.cs, TutorialImageSlider.cs, BlockScorer.cs, GameSession.cs, GameSceneManager.cs, ResultSequence.cs, Constants.cs, HintPanel.cs, Assets/Data/01~04_*Data.asset
- 확인 요청: 코드 리뷰(입력 타이밍·이벤트 해제·포맷 인자), EditMode/PlayMode 테스트, 콘솔 에러
- 결과: 통과. PlayMode 16/16 통과(EditMode 테스트 없음), 콘솔 에러 0. 기대 동작 5개 항목 모두 코드상 확인.
  - 지적 1(HintPanel level1TimeContainer 이름 비교): 오탐 — 연결 대상이 Level1Panel 자체라 이름이 일치함.
  - 지적 2(수력 결과 gateHeight null이면 '[]' 표시): 기존 코드. 조건 없이는 컴파일이 막혀 정상 플레이에서 발생하지 않음. 수정 보류.
  - 추가: Claude가 Play 모드에서 수력 힌트 확인 — 문제 높이 [3m]이 Text_Meter와 규칙 문구에 정상 표시.

### [2026-09-29] Claude → Antigravity · 사전 조사
- 확인 요청: 3_Game 힌트 Level3/4 패널, 1_Intro 시작 버튼, 4_Result 결과 텍스트 구조 조사
- 결과: 완료. HintPanel.level3MeterText가 씬에서 미연결(fileID 0) — 힌트 높이가 항상 10m로 표시되는 기존 버그 발견. 결과 텍스트는 604x300·55pt·자동 크기 끔·Overflow라 줄이 늘면 박스 밖으로 넘침. 1_Intro 패널에는 전체화면 raycast 이미지 없음.
