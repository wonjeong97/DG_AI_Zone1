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
