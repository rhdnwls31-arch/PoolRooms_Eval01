# Story 007 — 화면 전환 연출: Visual/Feel Evidence Sign-off

**Story**: `production/epics/depth-5m/story-007-screen-transition.md`
**Captured**: 2026-11-09
**Method**: 자동화 빌드+실행 스크린샷 (`commands.build` → `Builds/Win64/DEPTH5m.exe --scene Scene_Game --skiptofakeending --screenshot <path> --afterseconds <N>`).
`--skiptofakeending`은 이 스토리의 검증을 위해 `GameManager.cs`에 추가한 디버그 전용 명령줄 훅으로,
라운드를 거치지 않고 가짜 엔딩 접촉 상태로 즉시 건너뛴다(일반 플레이/빌드에는 영향 없음 — 인자를
명시적으로 넘길 때만 동작).

## 스크린샷

1. `01-noise-spike.png` (접촉 후 약 0.1초) — 접촉 트리거 직후, Film Grain이 스파이크 중인 시점의
   복도. 코드상 이 시점의 `FilmGrain.intensity.value`는 ramp 공식상 baseline(0.3)에서 peak(1.0)로
   가는 중간값(≈0.85~0.9)이어야 한다. 확대 크롭 확인 결과 벽 텍스처에 미세한 노이즈 텍스처가
   얹혀 있는 것은 보이나, 정적 스크린샷 한 장만으로는 "짧고 강한 피크"라는 체감(모션 중 보이는
   효과)까지는 증명되지 않는다 — Feel 절반은 사람이 실제 플레이로 확인해야 한다
   (`.claude/docs/run-and-observe.md`가 명시하는 정적 스크린샷의 한계).
2. `02-blackout.png` (접촉 후 약 1.0초) — 암전 거의 완료된 시점. 화면이 거의 완전히 검다(복도
   윤곽만 희미하게 남음).
3. `03-second-dream-fadein-mid.png` (접촉 후 약 1.1초) — 두 번째 꿈 공간(`Scene_SecondDream`)
   전환 직후, 페이드인이 진행 중인 중간 시점. 이전 프레임보다 눈에 띄게 밝아져 있다.
4. `04-second-dream-fadein-complete.png` (접촉 후 약 2.9초) — 페이드인이 끝나 완전히 밝아진
   두 번째 꿈 공간. 바닥+벽만 있는 최소 플레이스홀더 방(Story 008이 실제 레벨로 교체 예정)이
   또렷하게 보인다.

## 관찰된 특이사항 (코드 결함이 아니라 타이밍 측정상의 발견)

`02`(접촉+1.0초)는 거의 완전한 암전인데 `03`(접촉+1.1초, 불과 0.1초 뒤)은 이미 눈에 띄게
밝아져 있다 — `ScreenFader.fadeTime`을 2초로 설정했음에도 실제로는 훨씬 짧은 실시간 구간에
걸쳐 밝아지는 것으로 보인다. 코드 검토 결과 `SecondDreamSceneBuilder.cs`가 저장한
`fadeTime: 2`는 정확하고, `DreamTransitionController`/`IntroFadeIn`의 호출 순서도 의도대로다.
가장 유력한 원인은 **`Scene_SecondDream`의 머티리얼이 처음 렌더링되며 생기는 셰이더 컴파일
hitch**(새 씬의 머티리얼을 이 세션에서 처음 그리는 순간 한 프레임이 길게 멈추면서, 그 한 프레임
동안 코루틴의 경과 시간 계산이 한꺼번에 크게 건너뛰는 현상)로 추정된다. 이는 `DreamTransitionController`
자체의 로직 결함이 아니라 플레이어 빌드 첫 실행 시의 엔진 레벨 현상으로 보이며, 셰이더
워밍업(예: Shader Variant Collection 프리로드)으로 완화할 수 있다.

**이 사인오프가 책임지는 범위**: 전환의 **순서**(노이즈 증가 → 암전 → 씬 전환 → 페이드인 → 평소
노이즈로 복귀)가 코드와 스크린샷 양쪽에서 확인되었다는 것. 페이드인의 **정확한 소요 시간**이
항상 2초로 느껴지는지는 이 자동화 캡처만으로 확정할 수 없고, 사람이 직접 에디터에서 플레이하며
느낌을 확인해야 한다(Feel 절반, run-and-observe.md).

## 승인

- [x] AC-1 노이즈 증가: 코드/타이밍상 확인, 스크린샷은 보조 증거 (정적 컷의 한계 있음)
- [x] AC-2 암전: `02-blackout.png`로 확인
- [x] AC-3 페이드인 (Story 003 패턴 재사용): `03`→`04`로 확인, 정확한 "2초" 체감은 사람 확인 필요
- [x] AC-4 노이즈 평소 복귀: 코드상 baseline으로 ramp-down 후 블랙아웃 시작 (화면이 검게 가려진
      상태에서 복귀하므로 시각적으로 직접 보이진 않음 — 의도된 설계, Implementation Notes 참고)
- [x] AC-5 두 번째 꿈 공간 시작 지점: `04-second-dream-fadein-complete.png`로 확인 (Scene_SecondDream,
      Story 008 이전의 최소 플레이스홀더)

**Reviewed by**: self (solo mode), no independent review
