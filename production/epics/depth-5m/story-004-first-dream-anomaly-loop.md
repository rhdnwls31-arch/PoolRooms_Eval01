# Story 004: 첫 번째 꿈 — 기존 이상현상 시스템 연결

> **Epic**: DEPTH 5m
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-09

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

- [ ] **기존 `GameManager`/`CorridorTrigger`/`MannequinTracker` 로직은 코드 변경 없이 그대로 유지된다** (이 스토리는 연결/검증만 한다)
- [ ] `AnomalyManager`는 **단 하나의 변경만 허용**된다 — 랜덤 이상현상 풀에서 "마네킹이 달려온다"(case 1) 제외. 그 외 로직·수치는 변경하지 않는다
- [ ] 수심 5m에서 시작해 이상현상 **7종**이 정상적으로 등장한다 (달리는 마네킹은 등장하지 않는다)
- [ ] 이상 없음 → 앞문 전진 / 이상 있음 → 뒷문 복귀 판정이 기존과 동일하게 동작한다
- [ ] 정답 시 수심이 1m씩 감소하는 기존 동작이 유지된다
- [ ] "수심"이 UI/표지판 상에서 꿈의 깊이로 재해석되는 것 외에 판정 로직 변경은 없다 (텍스트/맥락상의 재해석이며 수치 로직은 동일)
- [ ] 오답 시 수심 리셋 및 정신력 감소 동작이 기존과 동일하다

---

## Implementation Notes

- 이 스토리는 새 기능을 만들지 않는다 — Story 003에서 연결한 씬 흐름 안에서 기존 `GameManager`/`AnomalyManager` 루프가 정상 동작하는지 **확인하고 연결을 완성**하는 것이 목적이다.
- GAME_DESIGN.md §4의 미확인 항목(물 셰이더/마네킹 접촉 Play 테스트, 이상현상 8종 원복 확인)이 있다면 이 스토리에서 함께 재확인한다.
- 수심 표지판 텍스트/연출에 "꿈의 깊이"라는 의미를 추가로 표기하고 싶다면 UI 텍스트만 수정 범위에 포함 — 판정 로직(GameManager의 수심 계산)은 건드리지 않는다.

- **달리는 마네킹 제외 (2026-11-09 설계 변경)**: `AnomalyManager`의 `case 1`(달려오는 마네킹)을
  랜덤 풀에서 뺀다. `TypeCount` 상수를 7로 줄이면 case 번호가 밀려 기존 로그·디버그와 어긋나므로,
  **case 번호는 그대로 두고 뽑기 대상만 배열로 명시**하는 편이 안전하다
  (예: `static readonly int[] RandomTypes = {0,2,3,4,5,6,7};`). `lastType` 연속 방지 로직은 그대로 동작한다.
  `extraMannequin`/`runner` 배선은 지우지 않는다 — Story 005/006이 가짜 엔딩에서 그대로 쓴다.

- **알려진 결함 (이 스토리 범위 밖, Story 006에서 수정)**: 달리는 마네킹의 Run 애니메이션이
  약 3초 후 멈춘 자세로 고정된 채 계속 다가온다. 조사 결과 **배제된 원인**: Animator 전이 없음
  (`m_Transitions: []`), Run 클립 `loopTime: 1`(루프 켜짐), `m_CullingMode: 0`(Always Animate),
  `m_ApplyRootMotion: 0`. **남은 단서 2개**: (1) `Scene_Game.unity`의 해당 Animator가
  `m_Avatar: {fileID: 0}` — Generic 리그(`animationType: 2`)인데 아바타 미할당,
  (2) `MannequinRunner.PlayAnim("Run")`이 `isMoving` 전환 시 단 한 번만 호출되고
  `PlayAnim`은 동일 상태면 무시하므로, Run에서 벗어나면 **되돌릴 코드가 없다**.
  근본 원인을 못 찾아도 달리는 동안 Run을 재확인하면 증상은 차단된다.
  위 설계 변경으로 이 마네킹은 첫 번째 꿈에 등장하지 않으므로 이 스토리에서는 재현되지 않는다.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 005]: 수심 1m 도달 이후의 새로운 분기(가짜 엔딩)

---

## Files

- `Assets/Scripts/AnomalyManager.cs` (modified — random anomaly draw narrowed to exclude case 1, per 2026-11-09 design change; TypeCount and switch cases left untouched)

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

---

## Completion Notes
**Completed**: 2026-11-09
**Criteria**: 7/7 passing
**Deviations**: OUT OF SCOPE (pre-existing, unrelated to this story) — `GameManager.cs`(fade-in 타이밍), `ScreenFader.cs`, `design/game-brief.md`, `EPIC.md`, `story-003.md`, `story-006.md`가 이전 세션(Story 003 등)부터 미커밋 상태로 남아있었음
**Test Evidence**: `production/qa/evidence/story-004-first-dream-anomaly-loop/01-scene-game-loaded.png` + 사용자 플레이 확인 (7종 이상현상 등장, 마네킹 미등장, 판정/수심감소/오답리셋/정신력감소 전부 정상)
**Reviewed by**: self, no independent review
