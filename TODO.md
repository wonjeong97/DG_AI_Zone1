# TODO

<!-- 이 프로젝트에서 해야 할 일. 사용자가 적고, Claude가 작업하면서 갱신한다(Antigravity는 읽기만 한다). -->
<!-- 형식: - [ ] 할 일 — 담당: Claude / 검증: Antigravity -->
<!-- 끝나면 [x]로 바꾸고 완료일을 붙여 "완료"로 옮긴다. 오래된 완료 항목은 지워도 된다(git 기록에 남는다). -->

## 진행 중

## 할 일

- [ ] (감사 후속) 로거를 주입받지 않는 컴포넌트의 Debug.Log 12곳을 ZLogger로 — ResultRowView, TypewriterTextTMP, CodingZoneZoom 등
- [ ] (감사 후속) GetComponentInChildren/InParent 2곳을 SerializeField로 — IntroSceneManager, CodingZoneZoom (씬 연결 필요)
- [ ] (감사 후속) 표시 전용 TMP 텍스트 Raycast Target 끄기 — 3_Game 약 22개, 4_Result 6개, 2_Story 5개, BlockFactory 블록 라벨
- [ ] (감사 후속) FlowInnerResize.LateUpdate 매 프레임 체인 순회를 변경 시에만 계산
- [ ] (감사 후속) 스냅 대상 선정 중복(UpdateSnapHighlight/TrySnapToSocket)을 한 메서드로
- [ ] 문제 문구·후보 값·정답을 Constants.Questions에서 LevelData(SO)로 옮기기 — 빌드 없이 바꿀 일 없어 JSON 대신 SO(2026-10-02 결정), 채점 점수·방향 이름은 Constants 유지
- [ ] (기획 확인) 미션 실패여도 다음 레벨이 해금되는 동작이 의도인지 — ResultSequence.OnNextClicked

## 완료

- [x] 스킬 준수 감사 1차 반영(타이머 버그와 회귀 테스트, 미사용 코드, 제어자 순서, 드래그 핫패스, LevelKind) — PR #61, 리뷰·PlayMode 19/19 통과 — 담당: Claude / 검증: Antigravity (2026-10-02)
- [x] 프로젝트 설정 정리: Player Settings Version을 수정일 26.10.1로 — 담당: Claude / 검증: Antigravity (2026-10-01)
- [x] 씬별 카메라 후처리 정리: UI 전용 씬(0_Title·1_Intro·2_Story·5_Outro) 후처리 끔, 4_Result의 3D 렌더텍스처 카메라 8개는 투명 배경 유지를 위해 끈 채 유지 — 담당: Claude / 검증: Antigravity (2026-10-01)
- [x] 게임 씬 도움말 버튼 문구를 '힌트'로 복원 — 담당: Claude (2026-09-29)
- [x] 레벨1~4 storyText 오타·\r 정리 — 담당: Claude (2026-09-29)
- [x] 수력 결과 '수문 닫기 조건(아니면)' ON/OFF (기획 A-9) — 담당: Claude (2026-09-29)
- [x] 수력 힌트 규칙 문구 추가 (기획 A-8) — 담당: Claude (2026-09-29)
- [x] 랜덤 문제 값 [ ] 표시 (기획 A-6) — 담당: Claude (2026-09-29)
- [x] 인트로 시작 버튼 제거·화면 터치 전환 (기획 A-4) — 담당: Claude (2026-09-29)
