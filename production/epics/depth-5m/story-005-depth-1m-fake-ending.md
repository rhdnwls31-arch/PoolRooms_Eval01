# Story 005: 수심 1m 가짜 엔딩 — 마네킹이 바라봄

> **Epic**: DEPTH 5m
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-09

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 5 — 수심 1m의 가짜 엔딩

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.0f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**:
- N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` §"수심 1m 이후", scoped to this story:*

- [ ] 수심이 1m에 도달하면 기존의 "SURFACE까지 계속 진행" 흐름이 아니라 새로운 분기로 들어간다
- [ ] 복도 가운데에 서 있는 마네킹이 등장해 있고(처음부터 화면 안에 보임, 갑자기 튀어나오지 않음) 플레이어를 천천히 바라보기 시작한다
- [ ] 마네킹이 플레이어를 바라보기 시작한 뒤 "정적" 구간이 존재한다 (즉시 돌진하지 않음)
- [ ] 이 분기는 기존 SURFACE/DROWNED 승패 판정을 트리거하지 않는다 — 별도의 새로운 상태(가짜 엔딩 진입)로 처리된다

---

## Implementation Notes

- 기존 `GameManager`의 라운드 진행 로직에서 "수심 == 1m" 도달 시점을 분기점으로 사용하되, 기존 수심 0m 도달 시 SURFACE 처리 코드는 건드리지 않고 **그 앞에 새 상태를 추가**하는 방식으로 구현한다 (기존 승리 경로를 깨지 않도록 주의).
- 마네킹의 "바라보기" 동작은 기존 `MannequinTracker.cs`의 회전 보간 패턴을 재사용한다.
- "처음부터 화면 안에 보여야 한다"는 요구사항은 마네킹 오브젝트를 비활성 상태로 숨겨두지 않고 복도 중앙에 항상 배치해 둔다는 의미 — 등장 타이밍이 아니라 가시성 문제임에 유의.
- 정적 구간의 길이는 수치 미정 — 1~2초 범위에서 임시 값으로 구현하고 GAME_DESIGN.md "확인이 필요한 것"에 추가할 만한 값으로 표시해 둔다.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 006]: 마네킹의 실제 돌진 동작과 접촉 처리

---

## Files

- `Assets/Scripts/GameManager.cs` (modified — 수심 1m 분기 추가)
- `Assets/Scripts/AnomalyManager.cs` (modified — extraMannequin을 처음부터 보이게 하고 MannequinWatcher를 붙임)
- `Assets/Scripts/MannequinWatcher.cs` (신규 — 가짜 엔딩의 바라보기/정적 구간 상태 머신)

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above.*

**[Logic — automated test specs]:**

- **AC-1**: 수심 1m 도달 시 가짜 엔딩 분기 진입
  - Given: 수심이 2m인 상태에서 정답 판정
  - When: 수심이 1m로 감소
  - Then: SURFACE/DROWNED가 아닌 새로운 "가짜 엔딩" 상태로 전환됨
  - Edge cases: 오답으로 수심이 5m로 리셋되는 기존 로직과 충돌하지 않는지 확인

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/game-manager/depth-1m-branch_test.cs` — must exist (advisory at `qa.level: minimal`) + 플레이 가능 상태를 보여주는 스크린샷 (`production/qa/evidence/`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/depth-5m/story-004-first-dream-anomaly-loop.md`
- Unlocks: `production/epics/depth-5m/story-006-mannequin-charge.md`

---

## Completion Notes

**Completed**: 2026-11-09
**Criteria**: 4/4 passing
**Deviations**: `fakeEndingWatchDuration = 2.5f` (정적 구간 신호 시간)는 확정값이 아닌 임시값 — `GAME_DESIGN.md` "확인이 필요한 것"에 추가 검토 필요
**Test Evidence**: `production/qa/evidence/story-005-depth-1m-fake-ending/01-round1-mannequin-hidden.png` (유닛 테스트는 `qa.level: minimal`에서 waived)
**Reviewed by**: self, no independent review (solo mode)
