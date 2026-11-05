# Unity — Deprecated APIs

Last verified: 2026-11-05

"Don't use X → Use Y" table, consolidated from the 2022 LTS → Unity 6.0 and
Unity 6.0 → 6000.6 migration notes (see `breaking-changes.md` for full context
and sources).

| Deprecated | Replacement | Notes |
|---|---|---|
| `Object.FindObjectsOfType<T>()` | `Object.FindObjectsByType<T>(sortMode)` | Now requires an explicit `FindObjectsSortMode` argument. Checked: not used in `Assets/Scripts/` as of this pin. |
| `Object.FindObjectOfType<T>()` | `Object.FindFirstObjectByType<T>()` or `Object.FindAnyObjectByType<T>()` | Pick `FindFirstObjectByType` when deterministic order matters, `FindAnyObjectByType` when any match is fine (faster). Checked: not used in `Assets/Scripts/` as of this pin. |
| `RenderPipelineEditorUtility.FetchFirstCompatibleTypeUsingScriptableRenderPipelineExtension()` | `GetDerivedTypesSupportedOnCurrentPipeline()` | Editor-only SRP utility; relevant only to custom editor tooling, not runtime gameplay code. |
| `CustomEditorForRenderPipelineAttribute`, `VolumeComponentMenuForRenderPipelineAttribute` | `CustomEditor` / `VolumeComponentMenu` combined with `SupportedOnRenderPipelineAttribute` | Editor/Volume authoring only. |
| `VisualElement.ExecuteDefaultAction()`, `ExecuteDefaultActionAtTarget()` | `HandleEventTrickleDown()`, `HandleEventBubbleUp()` | UI Toolkit only. This project uses in-world UI (lamps/signage), not UI Toolkit — likely not applicable, but check if UI Toolkit is introduced later. |
| `EventBase.PreventDefault()` | `StopPropagation()` | UI Toolkit only. |
| `GraphicsFormat.DepthAuto`, `ShadowAuto`, `VideoAuto` | explicit `GraphicsFormat` values | Now compile errors, not warnings — grep for these if any custom render-texture code is added. |
| Lighting window "Auto Generate" | `Lightmapping.Bake()` / `Lightmapping.BakeAsync()` | Editor workflow only. |
| `UPM_CACHE_PATH` env var | `UPM_CACHE_ROOT` | CI/build-machine environment variable, not project code. |

## Not sourceable at this pin

- A full list of **all ~190 breaking changes** mentioned for Unity 6000.6 is not
  reproduced here — only the subset the fetched release-notes summary surfaced.
  NOT SOURCEABLE — full changelog not fetched in this pass; see
  `https://docs.unity.com/en-us/engine/6000.6/manual/whats-new/unity66` for the
  complete list if a specific subsystem needs checking later.
- URP 17.6.0-specific API deprecations (as distinct from the general Unity 6.6
  engine changes above) were not separately researched this pass. NOT
  SOURCEABLE — revisit with a URP-specific changelog search if a URP API call
  looks suspect.
