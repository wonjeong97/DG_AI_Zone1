---
name: changelog
description: Update the project's root CHANGELOG.md following the Keep a Changelog format whenever a PR with visitor- or operator-facing changes is merged (or about to be merged), or when the user explicitly asks to update/write/generate the changelog (e.g. "체인지로그 갱신해줘", "CHANGELOG 업데이트해줘", "체인지로그 남겨줘"). Filters out internal refactors, typo fixes, and editor-only state changes; categorizes entries as Added/Changed/Fixed/Removed; calls out breaking changes separately at the top when present; keeps each entry to one sentence; and moves entries from `[Unreleased]` into a dated `## [YYYY-MM-DD]` section once merged to main.
---

# CHANGELOG.md 관리

이 프로젝트는 저장소 루트의 `CHANGELOG.md`에 [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/) 형식으로 변경 이력을 남긴다. 대상 독자는 관람객(전시 체험자)과 운영자(현장에서 콘텐츠를 켜고 다루는 사람) — 개발자 자신을 위한 리팩터링 메모가 아니다.

## 1. 언제 갱신하는가

- PR을 머지하기 직전이나 직후, 그 PR에 관람객·운영자 경험에 영향을 주는 변경이 하나라도 있으면 갱신한다.
- 사용자가 "체인지로그 갱신해줘" 등으로 명시적으로 요청하면, 마지막 CHANGELOG 항목 이후의 커밋 로그(`git log`)를 근거로 갱신한다.
- 영향 없는 변경만 있는 PR(리팩터링, 오타, 에디터 작업 상태 커밋 등)은 갱신하지 않는다 — 빈 항목을 억지로 만들지 않는다.

## 2. 무엇을 기록하고 무엇을 제외하는가

**기록 대상** — 관람객이 화면에서 체감하거나, 운영자가 현장에서 다루는 방식에 영향을 주는 변경:
- 새로운 연출/기능/화면 흐름 추가
- 기존 동작·연출·판정 방식 변경
- 눈에 보이거나 체험에 영향을 주는 버그 수정
- 운영 방식(자동 종료, 로그 전송, 설정 파일 위치 등)에 영향을 주는 변경

**제외 대상**:
- 내부 리팩터링, 코드 구조 변경 (동작 변화 없음)
- 오타 수정, 주석 정리, 변수명 변경
- 에디터 전용 작업 상태 커밋 (`chore: ... 에디터 작업 상태 반영` 류)
- 패키지 버전 갱신 자체 (단, 그 갱신으로 인해 실제 동작이 바뀌었다면 그 동작 변화를 기록)
- 아직 사용되지 않는 인프라를 미리 배치만 해둔 것 (비활성 상태로 남아 있다면 근거가 약하면 제외, 운영에 곧 영향을 줄 게 명확하면 짧게 포함)

애매하면 "관람객이나 운영자가 이 변경을 알아채거나 영향을 받는가?"로 판단한다.

## 3. 분류 (Added / Changed / Fixed / Removed)

- **Added**: 새로 생긴 기능·연출·화면
- **Changed**: 기존 동작의 방식이 바뀜 (판정 기준, 순서, 표시 방식 등)
- **Fixed**: 의도와 다르게 동작하던 것을 바로잡음
- **Removed**: 있던 기능·화면·연출이 없어짐

한 PR이 여러 카테고리에 걸치면 각 항목을 해당 카테고리 아래에 나눠 적는다. 없는 카테고리 헤더는 만들지 않는다.

## 4. Breaking Changes

세이브/진행도 데이터 형식, 설정 JSON의 키 이름·구조, 다른 콘텐츠(Zone)와 공유하는 API·이벤트 규약처럼 **기존 데이터나 연동을 깨뜨리는 변경**이 있으면, 카테고리 분류와 별개로 해당 섹션(Unreleased 또는 날짜 섹션) 맨 위에 `### ⚠ Breaking Changes`로 강조하고, 무엇이 깨지는지와 마이그레이션이 필요한지 한 문장으로 남긴다. 없으면 이 섹션 자체를 만들지 않는다.

## 5. 작성 방식

- 각 항목은 한 문장, 평서형으로 끝맺는다 (기존 CHANGELOG.md 항목들의 어조를 따른다).
- "왜"보다 "무엇이 바뀌었는가"를 관람객/운영자 시점에서 쓴다. 내부 구현(클래스명, 파일 경로)은 커밋 메시지에 남기고 CHANGELOG엔 넣지 않는다.
- 아직 main에 머지되지 않은 작업은 `## [Unreleased]`에 적는다.
- PR이 main에 머지되면, `[Unreleased]`에 있던 해당 항목을 지우고 머지 날짜 기준 `## [YYYY-MM-DD]` 섹션(이미 그 날짜 섹션이 있으면 같은 섹션에 추가)으로 옮긴다. 새 섹션은 파일에서 가장 위(최신순)에 놓는다.

## 6. 절차

1. 대상 범위 파악: 마지막으로 CHANGELOG에 반영된 시점 이후의 커밋(`git log <last-known-commit>..HEAD` 또는 병합될 PR의 커밋들)을 확인한다.
2. 각 커밋/PR 설명에서 2번 기준으로 기록 대상만 추린다.
3. 3번 카테고리로 분류하고, 4번 기준으로 breaking change 여부를 확인한다.
4. `CHANGELOG.md`를 5번 규칙대로 갱신한다 (Unreleased에 추가, 또는 머지 시점이면 날짜 섹션으로 이동).
