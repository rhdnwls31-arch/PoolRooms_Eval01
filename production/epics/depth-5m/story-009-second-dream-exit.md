# Story 009: 두 번째 공간 관찰 후 종료 흐름

> **Epic**: DEPTH 5m
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: S
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-10

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 10 — 두 번째 공간을 잠시 관찰한 뒤 종료

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.0f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**:
- N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` build order step 9, scoped to this story:*

- [ ] 플레이어가 두 번째 꿈 공간을 일정 시간 또는 특정 지점까지 탐색하면 현재 MVP 빌드의 종료 지점에 도달한다
- [ ] 종료 시 명확한 신호(화면 페이드, 텍스트, 또는 정적 화면 등)로 "이번 빌드는 여기까지"임을 알 수 있다 — 어색하게 끊기지 않는다
- [ ] 이 종료는 기존 SURFACE/DROWNED 승패 판정과 혼동되지 않는 별도의 상태다
- [ ] 이 스토리는 두 번째 꿈의 자체적인 클리어/실패 조건(향후 확장)을 구현하지 않는다 — 범위 밖

---

## Implementation Notes

- 가장 단순한 구현: 두 번째 공간 안에 "종료 트리거" 볼륨을 하나 배치하고, 닿으면 `ScreenFader.cs`로 페이드아웃 후 정적 "To be continued" 텍스트(또는 타이틀로 복귀)를 보여준다.
- 복잡한 엔딩 UI나 크레딧은 만들지 않는다 — 데모/프로토타입 종료 신호로 충분하다.
- `PauseMenu.cs`의 UI 표시 패턴(캔버스/텍스트 구조)을 참고해 최소한의 종료 화면을 구성할 수 있다.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- 두 번째 꿈의 승패 판정, 세 번째 꿈 이후 확장 — 이번 MVP 범위 밖

---

## Files

- `Assets/Scripts/DemoEndTrigger.cs` (new — exit trigger: fade out, then show the demo-end panel)
- `Assets/Editor/SecondDreamSceneBuilder.cs` (modified — places the DemoEndTrigger volume and the new DemoEndPanel UI in Scene_SecondDream)
- `Assets/Scenes/Scene_SecondDream.unity` (modified — regenerated via Unity batch mode, not hand-edited)
- `docs/engine-reference/unity/deprecated-apis.md` (modified — logged a newly confirmed Unity 6 API break found while implementing: `Resources.GetBuiltinResource<Font>("Arial.ttf")` -> `"LegacyRuntime.ttf"`)

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above.*

**[Integration — manual verification steps]:**

- **AC-1~3**: 종료 흐름
  - Setup: 두 번째 공간에서 종료 트리거까지 이동
  - Verify: 페이드아웃과 종료 신호가 명확하게 표시되는지, 기존 SURFACE/DROWNED 화면과 구분되는지 확인
  - Pass condition: 어색한 끊김 없이 "여기까지"임이 플레이어에게 전달됨

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: 문서화된 playtest (advisory) + `production/qa/evidence/`에 저장된 스크린샷

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/depth-5m/story-008-second-dream-space.md`
- Unlocks: `production/epics/depth-5m/story-010-screen-noise-staging.md`

---

## Completion Notes

**Completed**: 2026-11-10
**Criteria**: 4/4 passing (none deferred)
**Deviations**: Advisory — the retained trigger-fire evidence used a temporary (reverted) test spawn placed inside the trigger volume rather than a full natural WASD walkaround (synthetic input injection proved unreliable); recommend a quick human playtest of the natural path. Advisory — the Pool Hall's center-line path dead-ends at the dry basin's near wall (Story 008 scope, already closed); the side decks are the intended route but nothing currently cues the player to detour. Informational — found and fixed a Unity 6 API break (`Arial.ttf` → `LegacyRuntime.ttf`), logged in `docs/engine-reference/unity/deprecated-apis.md`.
**Test Evidence**: `production/qa/evidence/story-009-second-dream-exit/{01-hall-reachable,02-demo-end-screen}.png` + `stamp.txt` (waived at `qa.level: minimal`, screenshot not waived — run-and-observe). No separate `production/qa/playtests/` record.
**Reviewed by**: self, no independent review (solo mode)
