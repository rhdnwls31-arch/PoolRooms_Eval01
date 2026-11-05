# Story 010: 화면 노이즈 단계별 연출 전체 적용

> **Epic**: DEPTH 5m
> **Status**: Ready
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief §"화면 노이즈 연출" — 전체 빌드에 걸친 단계별 노이즈 적용 (build order step 10)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.0f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**:
- N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` §"화면 노이즈", scoped to this story:*

- [ ] 평소(도입부, 두 복도를 평범하게 걷는 동안)에는 매우 약한 Film Grain만 적용된다
- [ ] 이상현상 발생 지점 근처에서는 노이즈가 평소보다 조금 증가한다
- [ ] Story 002(낙하), Story 006/007(마네킹 돌진/전환) 같은 전환 순간에만 강한 노이즈가 적용된다 (이 피크 자체는 각 스토리에서 이미 구현됨 — 이 스토리는 "평소"와 "이상현상 근접" 두 단계를 추가하고 전체 단계가 일관되게 보간되도록 다듬는다)
- [ ] 화면을 가릴 정도의 지속적인 노이즈나 강한 깜빡임은 어떤 단계에서도 사용하지 않는다
- [ ] Chromatic Aberration, Vignette, Film Grain 세 가지 효과의 강도가 "평소 / 근접 / 전환" 세 단계에 맞게 각각 다른 값으로 설정되어 있다

---

## Implementation Notes

- 복잡한 VHS 전용 셰이더는 이번 범위에 포함하지 않는다 — URP Volume의 기본 Film Grain/Chromatic Aberration/Vignette 오버라이드만 사용한다.
- "평소/근접/전환" 세 단계를 하나의 공용 컨트롤러(예: `ScreenNoiseController.cs`)로 관리해, Story 002·006·007에서 만든 개별 전환 피크 로직과 평소/근접 단계가 충돌하지 않도록 통합한다.
- "이상현상 근접"의 판정 기준(거리? 타이머?)은 GAME_DESIGN.md에 명시되어 있지 않으므로, 임시 기준(예: 이상현상이 활성화된 라운드 동안 전체적으로 소폭 증가)으로 구현하고 추후 조정 가능하도록 Inspector 노출 값으로 둔다.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 007]: 전환 순간 자체의 노이즈 피크 로직(이미 구현됨, 이 스토리는 통합만)
- 복잡한 VHS 셰이더(Out of scope — brief 명시)

---

## Files

- `Assets/Scripts/ScreenNoiseController.cs` (신규)
- `Assets/Settings/Horror_PostFX.asset` (단계별 값 정리)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above.*

**[Visual/Feel — manual verification steps]:**

- **AC-1~5**: 노이즈 3단계 비교
  - Setup: 평소 보행 / 이상현상 라운드 중 / 전환 순간 세 구간을 각각 플레이
  - Verify: 세 단계의 강도 차이가 명확하고, 어느 단계에서도 화면을 가리거나 깜빡이지 않는지 확인
  - Pass condition: 세 단계가 시각적으로 구분되며 과도하지 않음

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: a retained screenshot (3단계 각각) in `production/qa/evidence/` + sign-off in `production/qa/evidence/screen-noise-staging-evidence.md`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/depth-5m/story-007-screen-transition.md`
- Unlocks: None (현재 MVP 범위의 마지막 스토리)
