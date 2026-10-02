# TODO

<!-- 이 프로젝트에서 해야 할 일. 사용자가 적고, Claude가 작업하면서 갱신한다(Antigravity는 읽기만 한다). -->
<!-- 형식: - [ ] 할 일 — 담당: Claude / 검증: Antigravity -->
<!-- 끝나면 [x]로 바꾸고 완료일을 붙여 "완료"로 옮긴다. 오래된 완료 항목은 지워도 된다(git 기록에 남는다). -->

## 진행 중

## 할 일

- [ ] (최종 영상 확정 후) 영상 오디오 트랙 제거 — 지금 Robot_260728.webm에 Unity가 지원하지 않는 Opus 오디오 트랙이 있어 인트로 재생 때 콘솔 에러가 남. 영상이 교체될 수 있어 최종 영상본이 정해지면 그 파일에서 ffmpeg -an으로 트랙을 지운다

## 완료

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
