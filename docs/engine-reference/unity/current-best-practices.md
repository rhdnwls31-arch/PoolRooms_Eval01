# Unity — Current Best Practices (post Unity 2022 LTS)

Last verified: 2026-11-05

Practices and defaults introduced since the LLM's training cutoff (~Unity 2022
LTS) that are new enough to be absent from, or contradicted by, training data.
Sources: see `breaking-changes.md`.

## Domain Reload is OFF by default (Unity 6.6+)

New projects created on 6000.6+ default **Enter Play Mode Settings** to
"Reload Scene only" instead of "Reload Domain and Scene." Effects:
- `static` fields and singletons are **not** automatically reset between Play
  sessions in the editor unless the project explicitly re-enables domain
  reload or the code resets its own statics.
- Code that relies on domain reload to reset state (a common Unity pattern
  from 2022-era tutorials and training data) can carry stale state into a new
  Play session in the editor only — builds are unaffected, since there is no
  domain reload concept at runtime outside the editor.
- **Action for this project**: this project (`AnomalyManager`, `GameManager`
  singletons) was not verified against this setting. Check
  `Edit > Project Settings > Editor > Enter Play Mode Settings` and, if
  domain reload is off, confirm round/state reset logic in `GameManager` does
  not implicitly depend on static fields being cleared between editor Play
  sessions.

## Burst is a built-in module

`Unity.Burst` no longer needs a separate package manager entry — it ships
with the editor. Not currently relevant (this project does not use Burst/Jobs),
noted for when `unity-dots-specialist` work begins.

## Mesh Read/Write is per-mesh, not project-wide

There is no global "force read/write" project setting anymore. If any future
work needs CPU access to mesh data (e.g., procedural water deformation beyond
the current shader-based `WaterScroll`/`Custom/PoolWater` approach), enable
Read/Write on that specific mesh's Import Settings, not a project default.

## DirectX 12 shader compiler (DXC) is available but not DX11-compatible

If this project's build or editor ever targets DirectX 11 explicitly, do not
enable DXC-only shader features — they will not compile on the DX11 path.
Not currently known to be in use here (grep found no explicit DXC references
in `Assets/`), noted for awareness given the project uses custom shaders
(`Custom/PoolWater`).

## Not evaluated this pass

- URP 17.6.0 Volume/post-processing API surface (this project uses 7
  post-processing effects via Volume) was not specifically re-verified against
  Unity 6.6 changes beyond the general engine notes above. NOT SOURCEABLE in
  this pass — if a Volume-related API call errors, check URP's own 17.x
  changelog before assuming the code is correct.
- New Input System-specific changes for Unity 6.6 were not separately
  researched. NOT SOURCEABLE — this project already uses the new Input System
  per `GAME_DESIGN.md`/`README.md`; revisit only if an Input System API call
  fails to compile.
