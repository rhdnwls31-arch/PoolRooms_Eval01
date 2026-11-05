# Story 006: 마네킹의 짧은 직선 돌진과 접촉 처리

> **Epic**: DEPTH 5m
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 6, 7 — 마네킹의 짧은 직선 돌진

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

- [ ] Story 005의 정적 구간이 끝나면 마네킹이 플레이어를 향해 짧은 직선으로 달려온다
- [ ] 마네킹에게 닿아도 즉사하거나 게임오버(DROWNED)로 처리되지 않는다
- [ ] 닿거나 충분히 가까워지는 순간 화면 전환 트리거가 발생한다 (전환 연출 자체는 Story 007에서 구현)
- [ ] 이 스토리의 범위는 "돌진 이동"과 "접촉/근접 감지"까지이며, 전환 연출·씬 로딩은 포함하지 않는다

---

## Implementation Notes

- 기존 `Assets/Scripts/MannequinRunner.cs`가 이미 "이상현상 1: 달리는 마네킹"의 Run 애니메이션/속도/접촉 거리 로직(속도 4.5, 접촉 거리 0.9, 보정 각도 -85도)을 구현하고 있다 — **이 가짜 엔딩용 돌진은 이상현상 시스템과는 별개의 스크립트 트리거이지만, 이동/애니메이션 구현은 `MannequinRunner.cs`의 기존 패턴을 최대한 재사용**한다 (새 이동 로직을 처음부터 만들지 않는다).
- 기존 이상현상용 마네킹 돌진과 혼동되지 않도록, 가짜 엔딩 전용 트리거(예: Story 005에서 추가한 분기 상태)에서만 이 돌진이 발동하도록 조건을 명확히 분리한다.
- "닿아도 게임오버 아님"을 보장하기 위해, 기존 `GameManager`의 정신력 감소/DROWNED 판정 코드 경로를 이 접촉 이벤트에 연결하지 않는다 — 별도의 이벤트(화면 전환 트리거)만 발생시킨다.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 005]: 바라보기/정적 구간
- [Story 007]: 접촉 이후의 실제 화면 전환 연출(노이즈/암전/눈뜨기)

---

## Files

- `Assets/Scripts/MannequinChargeTrigger.cs` (신규 — `MannequinRunner.cs`의 이동 패턴 재사용)
- (참고, 변경 없음) `Assets/Scripts/MannequinRunner.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above.*

**[Logic — automated test specs]:**

- **AC-1~2**: 돌진 및 비치명적 접촉
  - Given: 가짜 엔딩 분기 진입, 정적 구간 종료
  - When: 마네킹이 플레이어 방향으로 이동해 접촉
  - Then: DROWNED/정신력 감소가 발생하지 않고, 화면 전환 트리거 이벤트만 발생
  - Edge cases: 플레이어가 피해서 접촉이 늦어지는 경우에도 결국 트리거되는지(또는 범위 밖인지) 확인

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/mannequin/charge-trigger_test.cs` — must exist (advisory at `qa.level: minimal`) + 플레이 스크린샷 (`production/qa/evidence/`)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/depth-5m/story-005-depth-1m-fake-ending.md`
- Unlocks: `production/epics/depth-5m/story-007-screen-transition.md`
