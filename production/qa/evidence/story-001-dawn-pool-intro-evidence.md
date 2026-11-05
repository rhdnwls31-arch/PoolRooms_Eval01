# Evidence: Story 001 — 밤의 야외 수영장 도입부

**Date**: 2026-11-05
**Story**: `production/epics/depth-5m/story-001-dawn-pool-intro.md`

## Screenshots

- `01-night-pool-intro.png` — first capture (night direction). Defect: player faced +Z, showing only empty deck.
- `02-night-pool-intro-fixed.png` — after fixing player rotation to face -Z.
- `03-night-pool-intro-restructured.png` — after reorganizing scene hierarchy (`Dummies` group, `Water_Ambience`). Pixel-identical to 02 — reparenting was a true no-op on world positions.
- `04-dawn-pool-intro.png` — after converting night → bright dawn/dusk direction (user request), adding pool curb, reflection probe, warm directional light, and sky-band quads. Defect found: sky bands invisible (plain blue daytime sky) due to backface culling — the built-in Quad's front face (-Z normal) pointed away from the camera.
- `05-dawn-pool-intro-skyfixed.png` — after setting `_Cull: 0` on the three `Env_Sky_*` materials (and proactively on `Env_WindowGlow.mat`, same root cause). **This is the retained evidence image.**

## Prototype asset pipeline (user-directed, applies to all future DEPTH 5m scenes)

This project is in a prototype stage: placeholder primitives and no final sound
stand in for assets that will later be generated with **VARCO 3D** and **VARCO
Sound**. Per user instruction, `Scene_Intro.unity` was restructured after the
initial implementation to follow the convention going forward:

- All one-off placeholder props (`FlamingoTube`, `BuildingBackdrop`,
  `BuildingWindow_1/2/3`) are grouped under a new `Dummies` parent object
  (child of `Environment`), separate from the reused environment systems
  (`Deck`, `Water`) — so a later prefab swap only touches `Dummies`' children.
- The `AudioSource` for water ambience now lives on its own child object
  (`Water_Ambience`, under `Water`), not bundled onto the visual water mesh —
  so swapping the sound clip or the water mesh/material can happen
  independently.
- Player/camera baseline convention (for future stories to follow): **Player
  always spawns facing -Z toward the scene's focal point; `Main Camera` stays
  at local identity (position `(0, 1.6, 0)`, identity rotation) relative to
  `Player`.** To re-aim a new spawn, rotate `Player`, never the camera.
- No placeholder art (colors/shading) was polished — dummies are not final art.

## What's on screen (05-dawn-pool-intro-skyfixed.png — final)

- Bright pastel sky with a visible sun and pale orange/pink horizon bands — no longer plain daytime blue, reads as a soft dawn/dusk gradient (colors are muted/hazy rather than vivid, arguably fitting the "몽환적" dreamlike brief rather than a flaw — see note below)
- Pool clearly reads as a pool: visible curb/edge between `Deck` and `Water`, not a flat plane
- Water surface shows a visible reflection highlight (sun reflection, bottom-left) — `Env_Water.mat` smoothness increase + `PoolReflectionProbe` working
- Building backdrop with 3 warm window glows (`BuildingWindow_1/2/3`), now visible as pale warm rectangles (previously invisible due to the same backface-culling bug, fixed as a side effect)
- Flamingo tube floating on the water — now renders correctly pink (fixed by the brighter `DawnLight` reaching it; previously too dark under the dim night moonlight)
- Empty, calm, no people — composition matches "평화롭지만 비어 있는, 꿈결 같은" intent

**Open note**: the sky-band colors read as pale/washed-out pastel rather than a saturated sunset orange — likely emissive values are over-bright for the build's exposure/tonemapping. Left as-is since it still reads as "dreamy/hazy" rather than flatly wrong, and the brief's own tone is "몽환적" (dreamlike) rather than vivid — flagging for the user to judge rather than iterating further unprompted.

## Acceptance criteria check (current, dawn/dusk revision)

| AC | Result |
|---|---|
| 밝고 몽환적인 노을/새벽빛 야외 수영장에서 시작, 사람 없음 | OBSERVED — bright pastel scene, no NPC/human objects |
| 하늘이 보인다 (구름/노을·새벽빛) | OBSERVED, with a caveat — sky visible, sun visible, pale orange/pink bands present but muted/hazy rather than vivid (see note above) |
| 평평한 바닥이 아니라 수영장 구조로 읽힘 | OBSERVED — visible curb/edge around the water |
| 물 표면에 하늘/조명 반사 | OBSERVED — reflection highlight visible on the water surface |
| 수영장 조명 + 건물의 따뜻한 불빛 | OBSERVED — 3 warm window glows clearly visible |
| 조용한 물소리(플레이스홀더) | N/A to a screenshot — `AudioSource` on `Water_Ambience` is wired but clipless (no ambience asset exists yet in the project); called out, not a defect |
| 플라밍고 튜브가 물 위에 배치 | OBSERVED — correctly positioned and now correctly pink-colored |
| 밝고 아름답지만 고요한, 꿈결 같은 느낌 | OBSERVED, subjective — pastel, calm, empty; sky haziness arguably reinforces "dreamlike" |
| WASD로 이동 가능 (기존 PlayerController) | Not captured by a still — `PlayerController.cs` is unmodified and present on `Player`; this is a code-level claim, not something a screenshot proves. Recommend a quick manual Play-mode check before considering this fully closed. |

## Sign-off

Reviewed by: (developer, this session) — composition and required elements present after 3 fix-and-reverify rounds (player facing direction, scene restructuring, night→dawn conversion, backface-culling on sky/window quads). Two open items flagged for the user's judgment, not treated as blockers here: (1) sky-band colors read paler/hazier than a vivid sunset — may be acceptable as "dreamlike" or may need brighter/more saturated emission; (2) WASD movement not re-verified in Play mode this session.
