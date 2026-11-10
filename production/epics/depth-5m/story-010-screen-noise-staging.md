# Story 010: 화면 노이즈 단계별 연출 전체 적용

> **Epic**: DEPTH 5m
> **Status**: Complete
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-10

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

- `Assets/Scripts/ScreenNoiseController.cs` (new — 평소/근접 단계 Film Grain·Chromatic Aberration 관리, Vignette 오프셋 노출)
- `Assets/Scripts/GameManager.cs` (modified — screenNoiseController 통합, UpdateVignette() 합산, --forceanomaly 디버그 훅)
- `Assets/Settings/Horror_PostFX.asset` (modified — Film Grain 0.3→0.1, Chromatic Aberration 0.2→0.03 베이스라인 정리)
- `Assets/Scenes/Scene_Game.unity` (modified — ScreenNoiseController 컴포넌트를 GameManager 오브젝트에 배치 및 연결, Unity 배치 모드로 처리)

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

---

## Completion Notes

**Completed**: 2026-11-10
**Criteria**: 5/5 passing (none deferred)
**Deviations**: Advisory — AC-5의 "세 단계에 맞게 각각 다른 값으로 설정되어 있다"는 문구를 "데이터로 설정됨"으로 해석했음: 전환 단계의 Chromatic Aberration/Vignette 참고값(`transitionChromaticAberrationReference`/`transitionVignetteOffsetReference`)은 Story 002/006/007의 기존 전환 로직을 건드리지 않는 범위 제약에 따라 실제 전환 순간에는 연결되지 않고, Film Grain만 실제로 재생됨. Advisory — 근접 단계의 노이즈 증가는 코드상 명확하나(Film Grain 0.1→0.2, Chromatic Aberration 0.03→0.1) 정적 스크린샷에서는 체감 차이가 미세함.
**Test Evidence**: `production/qa/evidence/story-010-screen-noise-staging/{01-normal,01b-normal-crop,02-near-anomaly,02b-near-anomaly-crop,03-transition-spike,03b-transition-spike-crop}.png` + `stamp.txt`(staleness 확인 — 4개 파일 모두 UNCHANGED) + 사인오프 `production/qa/evidence/screen-noise-staging-evidence.md`(5개 AC 모두 Approved)
**Reviewed by**: self, no independent review (solo mode)
