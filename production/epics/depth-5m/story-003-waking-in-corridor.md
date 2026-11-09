# Story 003: 실내 수영장 복도에서 눈을 뜨는 연출

> **Epic**: DEPTH 5m
> **Status**: Complete
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: S
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-09

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 3 — 실내 수영장 복도에서 눈을 뜨는 연출

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.0f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**:
- N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` §"도입부"/"첫 번째 꿈", scoped to this story:*

- [x] 암전 이후 화면이 천천히 밝아지며 "눈을 뜨는" 느낌을 준다
- [x] 페이드인이 끝나면 플레이어는 기존 `Scene_Game.unity`의 수영장 복도 시작 지점에 위치한다
- [x] 페이드인 완료 후 플레이어 입력(이동/시점)이 다시 활성화된다
- [x] 기존 `GameManager`의 라운드 시작 로직이 정상적으로 트리거된다 (수심 5m, 정신력 3으로 초기화)

---

## Implementation Notes

- 기존 `ScreenFader.cs`의 페이드인 기능을 재사용한다.
- 씬 전환(도입부 씬 → `Scene_Game.unity`)은 `SceneManager.LoadScene` 등 가장 단순한 방식을 사용하고, 로드 완료 후 페이드인을 실행한다.
- `GameManager`의 초기화 로직은 기존 코드를 변경하지 않고 씬 시작 시 자동으로 실행되도록 연결만 한다.
- Story 002에서 비활성화한 입력을 여기서 다시 활성화한다.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 004]: 눈을 뜬 이후의 실제 이상현상 플레이 루프

---

## Files

- `Assets/Scripts/GameManager.cs` (씬 로드 후 초기화 트리거 연결 — 기존 로직 변경 없음)
- (사용) `Assets/Scripts/ScreenFader.cs` — 재사용

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above.*

**[Visual/Feel — manual verification steps]:**

- **AC-1~4**: 눈뜨기 전환과 초기화
  - Setup: Story 002의 암전 상태에서 이어서 재생
  - Verify: 페이드인 후 복도 시작 지점에 있고, 수심 5m/정신력 3으로 초기화되었는지 확인
  - Pass condition: 입력 복구, 올바른 초기 상태, 어색한 끊김 없음

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: a retained screenshot in `production/qa/evidence/` + sign-off in `production/qa/evidence/story-003-waking-in-corridor-evidence.md`

**Status**: [x] Created — `production/qa/evidence/story-003-waking-in-corridor/01-fade-in-mid.png` (+ 02, 03, 04)

---

## Dependencies

- Depends on: `production/epics/depth-5m/story-002-losing-consciousness.md`
- Unlocks: `production/epics/depth-5m/story-004-first-dream-anomaly-loop.md`

---

## Completion Notes

**Completed**: 2026-11-09
**Criteria**: 4/4 passing
**Deviations**: ADVISORY — `GameManager.cs`에 `firstFadeTime`(2.0초) 필드를 추가하고 `FirstRound()`의 `FadeIn()` 호출을 `FadeIn(firstFadeTime)`으로 변경함(Implementation Notes의 "기존 로직 변경 없이 연결만" 지시보다 넓은 수정 — AC-1 "천천히"를 만족시키기 위해 사용자가 승인한 선택). 라운드 전환(0.4s)·마네킹 피격(0.4s) 경로는 변경하지 않음. `ScreenFader.cs`에 `FadeIn(float)` 오버로드 신규 추가(기존 `FadeOut(float)`과 대칭, 기존 호출부 영향 없음). OUT OF SCOPE (같은 세션의 별개 작업, 이 스토리의 변경 아님): `design/game-brief.md`(수영장 진입/탈출 설계 의도 + 마네킹 제외 기록), `story-004-first-dream-anomaly-loop.md`, `story-006-mannequin-charge.md`, `docs/tech-debt-register.md`(신규, TD-001/TD-002 — Story 001 영역 Scene_Intro 콜라이더·수영장 사다리 부재).
**Test Evidence**: `production/qa/evidence/story-003-waking-in-corridor/` (4장) + sign-off `production/qa/evidence/story-003-waking-in-corridor-evidence.md`
**Reviewed by**: self, no independent review (solo mode)
