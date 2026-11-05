# Story 007: 화면 전환 연출 (노이즈 증가 → 암전 → 눈뜨기)

> **Epic**: DEPTH 5m
> **Status**: Ready
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 8 — 화면 전환

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.0f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**:
- N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` §"수심 1m 이후"/"화면 노이즈 연출", scoped to this story:*

- [ ] Story 006의 접촉 트리거가 발생하면 화면 노이즈(Film Grain 등)가 강하게 증가한다
- [ ] 노이즈 증가 직후 화면이 암전된다
- [ ] 암전 이후 눈을 뜨는 것과 같은 페이드인이 재생된다 (Story 003과 동일한 연출 패턴 재사용)
- [ ] 전환이 끝나면 노이즈가 평소 수준(매우 약한 Film Grain)으로 돌아온다
- [ ] 전환 완료 후 플레이어는 두 번째 꿈 공간(Story 008)의 시작 지점에 위치한다

---

## Implementation Notes

- 암전/페이드인은 `ScreenFader.cs`를 재사용한다 (Story 002, 003과 동일한 패턴).
- 노이즈 연출은 URP Volume의 Film Grain 효과 강도를 코루틴으로 보간해 구현한다 — 기존 `Settings/Horror_PostFX.asset` 볼륨 프로파일을 확장해서 사용한다.
- "화면을 가릴 정도의 지속적인 노이즈"나 "강한 깜빡임"은 사용하지 않는다 — 짧고 강한 피크 후 즉시 감소하는 곡선으로 구현한다.
- 씬 전환(복도 → 두 번째 꿈 공간)은 암전 구간 중에 로드해 전환이 매끄럽게 이어지도록 한다.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 008]: 두 번째 꿈 공간 자체의 레벨 디자인/분위기
- [Story 010]: 평소/근접 단계의 노이즈 연출(이 스토리는 전환 순간의 강한 노이즈만 다룸)

---

## Files

- `Assets/Scripts/DreamTransitionController.cs` (신규 — 노이즈 보간 + 씬 전환 + 페이드 연결)
- `Assets/Settings/Horror_PostFX.asset` (Film Grain 강도 범위 확장)
- (사용) `Assets/Scripts/ScreenFader.cs` — 재사용

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above.*

**[Visual/Feel — manual verification steps]:**

- **AC-1~5**: 전환 연출 전체 흐름
  - Setup: Story 006의 접촉 트리거 발생 직후
  - Verify: 노이즈 증가 → 암전 → 페이드인 → 노이즈 정상화 순서와 두 번째 공간 시작 위치 확인
  - Pass condition: 순서 뒤바뀜 없음, 화면이 완전히 가려질 정도로 과도하지 않음, 깜빡임 없음

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: a retained screenshot in `production/qa/evidence/` + sign-off in `production/qa/evidence/screen-transition-evidence.md`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/depth-5m/story-006-mannequin-charge.md`
- Unlocks: `production/epics/depth-5m/story-008-second-dream-space.md`
