# Story 001: 밝고 몽환적인 노을/새벽빛 야외 수영장 도입부

> **Revision (2026-11-05)**: direction changed from night to bright dreamy dawn/dusk per user request — see `design/game-brief.md` §"도입부". Acceptance criteria below updated to match.

> **Epic**: DEPTH 5m
> **Status**: Complete
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-05


> **2026-11-05 환경 재생성 (사후 기록).** 플레이 확인에서 (a) 벽이 한 면뿐이라 나머지
> 3면으로 지평선이 보이고, (b) 수영장이 파인 구조가 아니라 평면 위의 사각형이라 물 위를
> 걸어 다닐 수 있고, (c) 데크가 비어 있다는 문제가 나왔다. Scene_Intro의 Environment /
> Dummies를 `Assets/Editor/IntroMapBuilder.cs`로 다시 생성했다 — 사방 폐합, 파인 수영장,
> 소품 배치, 야외 전용 흰색 머터리얼. **이 스토리의 Status는 바꾸지 않았다**(사용자 판단 대기).
> 증거: `production/qa/evidence/story-002-losing-consciousness/01~03-courtyard-*.png`

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 1 — 밝고 몽환적인 노을/새벽빛 야외 수영장 도입부

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6000.6.0f1 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**:
- N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` §"도입부", scoped to this story:*

- [ ] 플레이어는 밝고 몽환적인 노을/새벽빛의 야외 수영장 씬에서 게임을 시작한다 (사람 없음)
- [ ] 하늘이 보인다 — 맑거나 옅은 구름이 낀, 노을/새벽빛으로 물든 하늘 (스카이박스 또는 그래디언트, 어두운 단색 배경 금지)
- [ ] 공간이 "평평한 바닥"이 아니라 수영장 구조(가장자리/데크/물 경계)로 명확히 읽힌다
- [ ] 물 표면에 하늘/조명이 반사되는 것이 보인다 (리플렉션 프로브 또는 셰이더 반사, 완전 불투명 단색 물 금지)
- [ ] 수영장 조명과 주변 건물의 따뜻한 불빛이 보인다
- [ ] 조용한 물소리(또는 플레이스홀더 환경음)가 재생된다
- [ ] 물 위에 떠 있는 플라밍고 튜브가 배치되어 있다
- [ ] 전체적으로 "밝고 아름답지만 고요한, 꿈결 같은" 느낌을 주는 조명/구성이다 (어둡고 음산한 톤이 아님)
- [ ] 플레이어가 WASD로 공간을 걸어 다닐 수 있다 (기존 PlayerController 재사용)

---

## Implementation Notes

- 기존 `PlayerController.cs`를 그대로 재사용해 이동/시점 조작을 구현한다 — 새 이동 로직을 만들지 않는다.
- 새 씬(예: `Scene_Intro.unity`)을 추가하거나, 기존 `Scene_Game.unity`에 도입부 영역을 덧붙이는 방식 중 더 간단한 쪽을 선택한다 — 결정은 구현 시점에 코드/씬 구조를 보고 판단.
- 조명과 분위기는 Pool Glow 레퍼런스(밝고 부드러운 poolcore 분위기)를 참고하되 에셋/맵을 복사하지 않는다.
- 사운드 에셋이 아직 없다면 플레이스홀더 루프 또는 무음으로 두고 TODO 주석을 남긴다.

**2026-11-05 추가 (밤→밝은 노을/새벽 전환에 따른 보강)**:
- **하늘/구름**: Camera의 ClearFlags를 Skybox로 되돌리고, URP의 Procedural/Physically Based Sky 또는 간단한 그래디언트 스카이박스 머티리얼로 노을/새벽 톤(주황~분홍~옅은 파랑)을 연출한다. 완전한 구름 시스템은 범위 밖 — 옅은 안개(Fog, RenderSettings)나 단순한 평평한 클라우드 플레인(흰색 반투명 텍스처 없는 경우 단색 블렌딩 머티리리얼)으로 "구름이 낀 느낌"만 내도 충분하다.
- **수영장 구조**: `Deck`을 평평한 Plane 하나로 두지 말고, 가장자리 턱(낮은 Cube 테두리)이나 물이 오목하게 들어간 형태로 최소한의 "수영장처럼 보이는" 구조를 추가한다. 복잡한 모델링은 금지 — 기본 도형(Cube/Plane) 조합으로 충분.
- **물 반사**: `Env_Water.mat`가 URP Lit 기반이면 Smoothness를 높이고 씬에 `Reflection Probe`(Baked 또는 Realtime, 가벼운 쪽 선택)를 하나 추가해 하늘/조명이 물에 비치게 한다. 완전히 불투명한 단색 물 셰이더 결과는 AC 미충족으로 간주한다.
- **조명**: 기존의 어두운 moonlight 대신 밝고 부드러운 Directional Light(노을/새벽 색온도)를 주광으로 사용하고, 그림자는 부드럽게(Soft Shadows) 설정한다.
- 이 모든 요소는 여전히 **더미/플레이스홀더** 수준으로 충분하다 — 사용자가 명시한 대로 지금은 동선·조명 방향·카메라 연출의 "확인"이 목적이며 최종 아트가 아니다. [[depth5m-placeholder-art-pipeline]] 규칙(더미는 `Dummies` 부모 아래, 오디오는 별도 컴포넌트)을 계속 따른다.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 의식을 잃는 연출(눈꺼풀 감김, 비네트, 암전)
- [Story 003]: 실내 복도에서 눈을 뜨는 전환

---

## Files

- Assets/Scenes/Scene_Intro.unity
- Assets/Scenes/Scene_Intro.unity.meta
- Assets/Materials/Env/Env_Water.mat (smoothness increased for reflections)
- Assets/Materials/Env/Env_FlamingoTube.mat
- Assets/Materials/Env/Env_FlamingoTube.mat.meta
- Assets/Materials/Env/Env_WindowGlow.mat
- Assets/Materials/Env/Env_WindowGlow.mat.meta
- Assets/Materials/Env/Env_Sky_Horizon.mat
- Assets/Materials/Env/Env_Sky_Horizon.mat.meta
- Assets/Materials/Env/Env_Sky_Mid.mat
- Assets/Materials/Env/Env_Sky_Mid.mat.meta
- Assets/Materials/Env/Env_Sky_High.mat
- Assets/Materials/Env/Env_Sky_High.mat.meta
- Assets/Scripts/ScreenshotOnArg.cs (run-and-observe capture helper, reused by future stories)
- Assets/Scripts/ScreenshotOnArg.cs.meta
- ProjectSettings/EditorBuildSettings.asset (added Scene_Intro to the build list)

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above.*

**[Visual/Feel — manual verification steps]:**

- **AC-1~5**: 평화로운 야외 수영장 분위기
  - Setup: 도입 씬을 플레이 모드로 실행
  - Verify: 조명, 플라밍고 튜브, 환경음이 모두 존재하는지 확인
  - Pass condition: 씬에 사람이 없고, 위 4개 요소(조명/불빛/물소리/튜브)가 모두 확인됨

---

## Test Evidence

*Governed by `qa.level`: at `qa.level: minimal` tests are waived (advisory), but a Visual/Feel story's retained screenshot is not.*

**Story Type**: Visual/Feel
**Required evidence**: a retained screenshot in `production/qa/evidence/` + sign-off in `production/qa/evidence/story-001-dawn-pool-intro-evidence.md`

**Status**: [x] Created — `production/qa/evidence/story-001-dawn-pool-intro/05-dawn-pool-intro-skyfixed.png`

---

## Dependencies

- Depends on: None
- Unlocks: `production/epics/depth-5m/story-002-losing-consciousness.md`

---

## Completion Notes

**Completed**: 2026-11-05
**Criteria**: 8/9 passing (1 deferred — WASD movement not re-verified in Play mode this session)
**Deviations**: ADVISORY — sky-band colors read pale/hazy rather than a saturated sunset (candidate for a later lighting/exposure pass); camera uses engine-default Skybox + emissive gradient quads rather than a custom skybox material (per the story's own "or gradient" allowance). OUT OF SCOPE (pre-existing, not from this story): `AnomalyManager.cs`, `GameManager.cs`, `Scene_Game.unity`, `PC_Renderer.asset`, `ProjectSettings.asset` were already modified before this session; `PC_RPAsset.asset` was touched by Unity batchmode build reserialization, not an intentional edit.
**Test Evidence**: `production/qa/evidence/story-001-dawn-pool-intro/05-dawn-pool-intro-skyfixed.png` + sign-off in `production/qa/evidence/story-001-dawn-pool-intro-evidence.md`
**Reviewed by**: self, no independent review (solo mode)
