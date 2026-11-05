# Story 004: 첫 번째 꿈 — 기존 이상현상 시스템 연결

> **Epic**: DEPTH 5m
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 4 — 기존 이상현상 관찰 플레이 (첫 번째 꿈)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.0f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**:
- N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` §"첫 번째 꿈", scoped to this story:*

- [ ] **기존 `AnomalyManager`/`GameManager`/`CorridorTrigger`/`MannequinTracker` 로직은 코드 변경 없이 그대로 유지된다** (이 스토리는 연결/검증만 한다)
- [ ] 수심 5m에서 시작해 기존 8종 이상현상이 정상적으로 등장한다
- [ ] 이상 없음 → 앞문 전진 / 이상 있음 → 뒷문 복귀 판정이 기존과 동일하게 동작한다
- [ ] 정답 시 수심이 1m씩 감소하는 기존 동작이 유지된다
- [ ] "수심"이 UI/표지판 상에서 꿈의 깊이로 재해석되는 것 외에 판정 로직 변경은 없다 (텍스트/맥락상의 재해석이며 수치 로직은 동일)
- [ ] 오답 시 수심 리셋 및 정신력 감소 동작이 기존과 동일하다

---

## Implementation Notes

- 이 스토리는 새 기능을 만들지 않는다 — Story 003에서 연결한 씬 흐름 안에서 기존 `GameManager`/`AnomalyManager` 루프가 정상 동작하는지 **확인하고 연결을 완성**하는 것이 목적이다.
- GAME_DESIGN.md §4의 미확인 항목(물 셰이더/마네킹 접촉 Play 테스트, 이상현상 8종 원복 확인)이 있다면 이 스토리에서 함께 재확인한다.
- 수심 표지판 텍스트/연출에 "꿈의 깊이"라는 의미를 추가로 표기하고 싶다면 UI 텍스트만 수정 범위에 포함 — 판정 로직(GameManager의 수심 계산)은 건드리지 않는다.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 005]: 수심 1m 도달 이후의 새로운 분기(가짜 엔딩)

---

## Files

- (검증만, 변경 없음) `Assets/Scripts/GameManager.cs`, `Assets/Scripts/AnomalyManager.cs`, `Assets/Scripts/CorridorTrigger.cs`, `Assets/Scripts/MannequinTracker.cs`
- `Assets/Scripts/WaterScroll.cs` (물 연출 Play 테스트 대상, 변경 없음 — GAME_DESIGN.md §4 확인 항목)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above.*

**[Integration — manual verification / playtest]:**

- **AC-1~6**: 8종 이상현상 전체 루프
  - Setup: Story 003 완료 지점부터 플레이 시작
  - Verify: 8종 이상현상을 각각 최소 1회 이상 발생시켜 판정/수심/정신력 동작을 확인
  - Pass condition: 기존 README/GAME_DESIGN.md에 기술된 규칙과 100% 일치, 원복 누락 없음

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are waived (advisory), but this story's player-observable behaviour still needs a retained screenshot per the run-and-observe rule.*

**Story Type**: Integration
**Required evidence**: `tests/integration/anomaly-loop/` 자동 테스트 OR 문서화된 playtest (advisory) + `production/qa/evidence/`에 저장된 스크린샷

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/depth-5m/story-003-waking-in-corridor.md`
- Unlocks: `production/epics/depth-5m/story-005-depth-1m-fake-ending.md`
