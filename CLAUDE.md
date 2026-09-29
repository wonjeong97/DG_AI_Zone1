# CLAUDE.md

@.agents/AGENTS.md

공통 규칙은 위에서 불러온 `.agents/AGENTS.md`가 원본이다. Claude 전용 규칙만 이 파일에 추가한다.

## Claude 전용

- 멀티 에이전트 구성에서 Claude는 **지휘 및 구현 담당**이다(`.agents/AGENTS.md`의 Multi-Agent Workflow 참고).
- 스킬을 수정할 때는 `.claude/skills/`가 아니라 `.agents/skills/`를 수정한 뒤 `tools/sync-agent-skills.ps1`을 실행한다.
