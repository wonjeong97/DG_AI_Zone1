# AGENTS.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

---

## Project Convention: CHANGELOG.md

관람객·운영자 경험에 영향을 주는 변경이 머지될 때마다 저장소 루트의 `CHANGELOG.md`를 갱신할 것.

- [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/) 형식(`Added`/`Changed`/`Fixed`/`Removed`)을 따름.
- 내부 리팩터링, 오타 수정, 에디터 전용 작업 상태 등 사용자에게 영향 없는 변경은 기록하지 않음.
- 호환성이 깨지는 변경이 있으면 해당 섹션 최상단에 별도로 강조함.
- 각 항목은 한 문장으로 간결하게 작성함.
- 아직 main에 머지되지 않은 변경은 `[Unreleased]` 섹션에 기록하고, 머지되면 날짜 섹션(`## [YYYY-MM-DD]`)으로 옮김.
- 상세 절차는 `.agents/skills/changelog/SKILL.md` 참고.

---

## Multi-Agent Workflow (Claude + Antigravity)

이 저장소는 Claude(Claude Desktop / Claude Code)와 Antigravity(데스크톱 앱 / `agy` CLI)가 같은 작업 폴더와 같은 Unity Editor를 공유한다. 이 파일이 두 에이전트 공통 규칙의 원본이며, `CLAUDE.md`는 이 파일을 불러온다.

### 역할

- **사용자 — 최종 결정**: 요구사항, 우선순위, 머지 여부를 정한다.
- **Claude — 지휘 및 구현**: 작업 분해·배분, C# 스크립트 구현·리팩터링, 씬·프리팹·에셋 수정. 검증이 필요하면 `agy` CLI를 호출해 Antigravity에 맡긴다.
- **Antigravity — 검증**: 콘솔 에러 확인, 테스트 실행, 씬·오브젝트 상태 조회, 코드 리뷰, 문서·API 조사. 코드·에셋은 수정하지 않고 결과만 보고한다.
- 사용자가 다르게 지시하면 그 지시를 따른다.

### Unity MCP 규칙

- Unity Editor는 하나다. **쓰기 작업은 한 번에 한 에이전트만** 한다(기본: Claude). 쓰기 작업이란 씬·게임오브젝트·컴포넌트·프리팹·에셋·스크립트를 생성·수정·삭제하거나 저장하는 모든 MCP 호출을 말한다.
- 검증 담당은 조회 계열 호출(`read_console`, `find_gameobjects`, `find_in_file`, `validate_script`)과 `run_tests`만 사용한다.
- 스크립트를 수정한 뒤에는 컴파일·도메인 리로드가 끝날 때까지(`editor_state`의 `isCompiling`이 false) MCP 명령을 보내지 않는다.
- 작업을 마치면 `read_console`로 에러가 없는지 확인한다.
- Play 모드 진입·종료는 현재 쓰기 담당 에이전트만 한다.

### Claude → Antigravity 호출 (`agy` CLI)

- 검증을 맡길 때는 저장소 루트에서 다음 형식으로 호출한다.

  ```bash
  agy -p "<요청>" --model gemini-3.8-flash-medium --print-timeout 300s < /dev/null
  ```

- `--dangerously-skip-permissions`는 사용하지 않는다. 권한은 `~/.gemini/antigravity-cli/settings.json`의 허용·차단 목록으로 관리한다(원본: `docs/agents/agy-settings.json`). 이 설정은 파일 쓰기와 Unity MCP 쓰기 도구를 차단한다.
- 요청에는 확인할 대상(변경 파일, 기대 동작), 사용할 도구, 보고 형식(통과/실패와 근거)을 명시한다.
- 호출 결과는 요약해서 `docs/agents/HANDOFF.md`에 기록한다.
- 어려운 판단(설계 리뷰 등)만 `gemini-3.1-pro-high`를 쓰고, 나머지는 Flash 모델로 사용량을 아낀다.

### 작업 인계

- `docs/agents/TASKS.md`: 작업 보드. 작업을 시작할 때 담당과 상태를 갱신한다.
- `docs/agents/HANDOFF.md`: 다른 에이전트에게 넘기거나 검증 결과를 받을 때 변경 파일, 확인 요청 사항, 결과를 기록한다.
- `docs/agents/REVIEW_ITEMS.md`: SW 수정 검토사항(PDF)과 기획자 전달 사항의 항목별 진행 현황·결정 사항. 검토사항 작업을 시작하기 전에 읽고, 끝내면 상태를 갱신한다.
- 작업 단위마다 커밋한다. 다른 에이전트가 커밋하지 않은 변경은 건드리지 않는다.

### 스킬 관리

- 스킬 원본은 `.agents/skills/`다. `.claude/skills/`는 Claude가 읽을 수 있도록 둔 복사본이므로 직접 수정하지 않는다.
- 스킬을 추가·수정·삭제한 뒤에는 `tools/sync-agent-skills.ps1`을 실행해 복사본을 갱신하고, 두 폴더를 함께 커밋한다.
