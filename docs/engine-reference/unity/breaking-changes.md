# Unity — Breaking Changes

Last verified: 2026-11-05

Scope: Unity 2022 LTS → Unity 6.0 LTS, and Unity 6.0 → 6000.6 (Unity 6.6).
This project is pinned to **6000.6.0f1**, so both spans apply if any code or
assets originated against an older Unity version.

## 2022 LTS → Unity 6.0

Source: [Upgrade to Unity 6.0 LTS](https://docs.unity3d.com/6000.1/Documentation/Manual/UpgradeGuideUnity6.html)

### Rendering & Lighting
- Gaussian filter radius properties (`filteringGaussRadiusAO`, `filteringGaussRadiusDirect`, `filteringGaussRadiusIndirect`) changed from integer to floating-point.
- Light Probes are ~6% brighter by default (energy conservation change). Multiply coefficient 0 by 16/17 to restore old appearance if needed.
- Enlighten baking removed entirely; Progressive Lightmapper is used automatically.
- The "Auto Generate" lighting setting is removed — bake explicitly via `Lightmapping.Bake()` / `Lightmapping.BakeAsync()`.
- `GraphicsFormat.DepthAuto`, `ShadowAuto`, and `VideoAuto` are obsolete and now cause compile errors.
- Metal: `min16float`/`half`/`real` buffer types now map to 32-bit floats instead of 16-bit.
- Environment lighting (ambient probe, skybox reflection probe) is no longer baked by default.

### Scripting API
- `Object.FindObjectsOfType()` → `Object.FindObjectsByType()` (now requires a sort-mode parameter).
- `Object.FindObjectOfType()` → `Object.FindFirstObjectByType()` or `Object.FindAnyObjectByType()`.
- SRP: `RenderPipelineEditorUtility.FetchFirstCompatibleTypeUsingScriptableRenderPipelineExtension()` → `GetDerivedTypesSupportedOnCurrentPipeline()`.
- `CustomEditorForRenderPipelineAttribute` / `VolumeComponentMenuForRenderPipelineAttribute` → combine `CustomEditor`/`VolumeComponentMenu` with `SupportedOnRenderPipelineAttribute`.

### UI Toolkit
- `ExecuteDefaultAction()` / `ExecuteDefaultActionAtTarget()` → `HandleEventTrickleDown()` / `HandleEventBubbleUp()`.
- `PreventDefault()` → `StopPropagation()`.

### Textures
- Runtime 2D textures are no longer affected by mipmap limits by default; opt in via the `MipmapLimitDescriptor` constructor parameter.

### Android
- `UnityPlayer` Java class replaced by `UnityPlayerForActivityOrService` / `UnityPlayerForGameActivity`; `UnityPlayer` no longer extends `FrameLayout` (use `getFrameLayout()`).
- Default tool versions raised: Gradle 8.4, Android Gradle Plugin 8.3.0, JDK 17.
- NOT SOURCEABLE — exact relevance to this project (which does not currently target Android) not evaluated.

### Package management
- `UPM_CACHE_PATH` env var no longer supported → use `UPM_CACHE_ROOT`.
- The global cache `packages` folder is deprecated.

## Unity 6.0 → 6000.6 (Unity 6.6)

Source: [What's new in Unity 6000.6](https://docs.unity.com/en-us/engine/6000.6/manual/whats-new/unity66)

### Rendering
- Dynamic batching removed (obsolete feature).
- URP can read the depth buffer from GPU memory via input attachments — DirectX 12 / Vulkan only.
- A new DirectX 12 shader compiler (DXC) is available; **DXC is incompatible with DX11**.

### Programming / project defaults
- **Domain Reload is OFF by default for new projects** (Enter Play Mode Settings default changed to "Reload Scene only"). This project predates 6000.6 — confirm `Edit > Project Settings > Editor > Enter Play Mode Settings` reflects the intended behavior, since any `static` field reset logic written assuming domain reload may now behave differently.
- Burst compiler is now a built-in module (`Unity.Burst` namespace) — no package install required.
- Preprocessor symbols `UNITY_64` and `DEVELOPMENT_BUILD` as used in some older snippets are called out as changed/removed in migration notes — NOT SOURCEABLE in full detail from the fetched page; verify any use of these symbols directly against current docs before relying on them.

### Assets / build
- Mesh Read/Write can no longer be force-enabled via a project-wide setting; it must be set per-mesh in Import Settings, and a build fails if a mesh needs it but it is disabled.
- The `Library/Artifacts` folder is renamed to `Library/DataStore`.

### Platform
- macOS: Intel (x86_64) editor/player support deprecated — Apple Silicon only going forward. Not relevant to this project's Windows-only build target, noted for completeness.
- Android: legacy/round icon support removed (adaptive icons only); OpenGL ES minimum raised to 3.1. Not currently targeted by this project.

### Packages promoted to core
- Animation Rigging, Timeline, and Cinemachine are now core packages (no separate package manager entry needed for the base functionality).

## Not yet checked against this project's code

This document was built from general Unity 2022→6/6.6 migration sources, not
from a line-by-line audit of `Assets/Scripts/`. Before relying on it for a
specific file, confirm the API in question is actually used there (e.g. grep
for `FindObjectOfType`, `FindObjectsOfType` in `Assets/Scripts/`).
