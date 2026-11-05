# Story 008: 두 번째 꿈 공간 제작

> **Epic**: DEPTH 5m
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 9 — 기존 복도와 다른 두 번째 꿈 공간

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.0f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**:
- N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` §"두 번째 꿈 공간", scoped to this story:*

- [ ] 첫 번째 복도의 단순 변형(소품 하나만 옮긴 방)이 아니라, 플레이어가 "다른 공간으로 이동했다"고 느낄 만큼 구조가 다르다
- [ ] 공간은 **소규모**로 제작한다 — 처음부터 대형 맵을 만들지 않는다
- [ ] 기존 에셋(머티리얼, 텍스처, 소품)을 최대한 재사용한다
- [ ] 다음 중 최소 2가지 이상의 "섬뜩함" 요소를 포함한다: 끝이 보이지 않는 수영장, 너무 넓고 비어있는 느낌, 따뜻한 빛/차가운 물빛 대비, 물이 있어야 할 자리에 바닥, 공간 일부의 비정상적 반복, 멀리 서 있는 마네킹 한 명, 보이지 않는 라디오 소리
- [ ] 플레이어가 WASD로 이 공간을 걸어 다닐 수 있다 (기존 PlayerController 재사용)

---

## Implementation Notes

- 새 씬(`Scene_SecondDream.unity`) 또는 기존 `Scene_Game.unity` 내 별도 영역으로 구현 — Story 001과 동일한 판단 기준(더 간단한 쪽) 적용.
- 재질/텍스처는 `Assets/Materials/Env/`, `Assets/Textures/Env/`의 기존 에셋을 재사용한다.
- "라디오 소리는 들리지만 라디오가 보이지 않음" 같은 요소는 기존 오디오 소스를 숨겨진 위치에 배치하는 것만으로 구현 가능 — 새 시스템 불필요.
- "공간 일부만 비정상적으로 반복됨"은 기존 복도 모듈(벽/바닥 프리팹이 있다면)을 재배치하는 방식으로 저비용으로 구현한다.
- 범위를 넘는 대형 레벨 디자인 작업이 필요하다고 판단되면 이 스토리를 더 작은 스토리로 쪼개는 것을 고려하되, 쪼개기는 `/dev-story` 진행 중 판단으로 남겨둔다.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 009]: 이 공간에서의 관찰 후 종료 흐름(클리어 조건)
- [Story 010]: 이 공간의 노이즈 연출 세부 조정(기본 포스트프로세싱은 포함 가능하되 단계별 노이즈 다듬기는 제외)

---

## Files

- `Assets/Scenes/Scene_SecondDream.unity` (신규, 또는 `Scene_Game.unity` 확장)
- (재사용) `Assets/Materials/Env/*`, `Assets/Textures/Env/*`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above.*

**[Integration — manual verification / playtest]:**

- **AC-1~5**: 공간 체감과 요소 확인
  - Setup: Story 007의 전환 완료 지점에서 시작
  - Verify: 공간이 첫 번째 복도와 명확히 다르게 느껴지는지, 섬뜩함 요소가 최소 2개 이상 존재하는지 확인
  - Pass condition: 플레이테스터(개발자 본인 포함)가 "같은 복도의 변형"이 아니라고 느낌

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are waived (advisory), but this story's player-observable result still needs a retained screenshot per the run-and-observe rule.*

**Story Type**: Integration
**Required evidence**: 문서화된 playtest (advisory) + `production/qa/evidence/`에 저장된 스크린샷

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/depth-5m/story-007-screen-transition.md`
- Unlocks: `production/epics/depth-5m/story-009-second-dream-exit.md`
