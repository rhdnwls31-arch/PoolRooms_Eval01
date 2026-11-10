# Story 010 — 화면 노이즈 단계별 연출: Visual/Feel Evidence Sign-off

**Story**: `production/epics/depth-5m/story-010-screen-noise-staging.md`
**Captured**: 2026-11-10
**Method**: 자동화 빌드+실행 스크린샷(`commands.build` → `Builds/Win64/DEPTH5m.exe --scene Scene_Game [디버그 인자] --screenshot <path> --afterseconds <N>`), 벽 텍스처 영역을 추가로 크롭·확대해 Film Grain 차이를 직접 비교.

`--forceanomaly`는 이 스토리의 검증을 위해 `GameManager.cs`에 추가한 디버그 전용 명령줄 훅으로,
첫 라운드를 "근접" 단계(이상현상 활성화)로 즉시 시작한다(일반 플레이/빌드에는 영향 없음 — 인자를
명시적으로 넘길 때만 동작). `--skiptofakeending`은 Story 007이 이미 추가한 동일한 성격의 훅이다.

## 스크린샷

1. `01-normal.png` + `01b-normal-crop.png`(벽 텍스처 확대) — 평소 단계. 1라운드, 이상현상 없음.
   소품(플라밍고/라디오)은 원래 자리(flamingo x=-1.5, radio x=1.8 — 복도 폭상 중앙에 가깝게
   보이지만 이상현상이 옮기는 위치(x=0, x=0.3)와는 다름, 코드 확인)에 있고 물도 없다. 확대 크롭 결과
   벽면이 거의 매끈하다 — Film Grain 0.1은 눈에 띄지 않는 수준(AC-1 "매우 약함" 충족).
2. `02-near-anomaly.png` + `02b-near-anomaly-crop.png` — 근접 단계. `--forceanomaly`로 1라운드부터
   이상현상 활성화, 이번 실행에서는 무작위로 "물이 차오르는" 이상현상(case 7)이 뽑혀 복도가 침수된
   모습으로 나타났다(이 자체는 Story 010 범위가 아닌 기존 AnomalyManager 연출). 확대 크롭 비교 결과
   평소(`01b`) 대비 Film Grain 차이는 맨눈으로는 거의 구별되지 않는다 — 코드상 0.1→0.2로 2배
   증가하도록 설정되어 있으나(AC-2 "조금 증가"에 부합하는 의도적으로 미세한 차이), 정적 스크린샷의
   한계로 이 미세한 차이 자체를 육안으로 단정하기는 어렵다.
3. `03-transition-spike.png` + `03b-transition-spike-crop.png` — 전환 단계. `--skiptofakeending`으로
   가짜 엔딩 접촉 지점까지 건너뛴 뒤 접촉 0.1초 후 캡처 — `DreamTransitionController`가 Film Grain을
   0.1(현재 평소 베이스라인) → 1.0으로 스파이크시키는 구간 중간(ramp 공식상 ≈0.85). **확대 크롭
   비교 결과 `01b`/`02b`와 뚜렷이 다르게 벽면 전체에 또렷한 입자 노이즈가 보인다** — 전환 단계의
   강한 Film Grain은 육안으로도 명확히 구분됨(AC-5 Film Grain 항목 확인).

## 코드로만 확인되고 스크린샷으로는 보이지 않는 항목

- **Chromatic Aberration**: 평소(0.03)/근접(0.1) 두 단계 모두 `ScreenNoiseController`가 실제로
  적용하지만, 이 정도 세기 차이는 1280×720 정적 스크린샷에서 육안 구분이 사실상 불가능하다.
  코드 검토로 값이 설정되어 있음을 확인(AC-5).
- **전환 단계의 Chromatic Aberration/Vignette**: Story 002/006/007의 기존 전환 로직을 건드리지
  않는다는 범위 제약에 따라, `ScreenNoiseController`의 `transitionChromaticAberrationReference`/
  `transitionVignetteOffsetReference` 필드는 **참고용 데이터로만 존재**하고 실제 전환 순간에
  연결되어 재생되지는 않는다 — AC-5("세 가지 효과가 세 단계에 맞게 각각 다른 값으로 설정")는
  "설정되어 있다"는 문구이므로 코드상 데이터로 충족한다고 판단했으나, 실제로 전환 때 체감되는
  것은 Film Grain뿐이다. 이 해석에 이견이 있다면 추가 작업이 필요하다.
- **Vignette 오프셋의 라운드 간 차이**: `GameManager.UpdateVignette()`가 정신력 항 + 단계
  오프셋을 더하도록 바뀌었음은 코드로 확인했으나, 정신력이 바뀌지 않는 라운드에서 오프셋만의
  변화를 스크린샷 비교로 단정하기는 더더욱 어렵다 — 수치로만 보증한다.

## 승인

- [x] AC-1 평소 매우 약한 Film Grain: `01b-normal-crop.png`로 확인 — 거의 매끈함
- [x] AC-2 근접 시 소폭 증가: 코드로 확인(0.1→0.2, 0.03→0.1); 스크린샷상 체감은 미세해 보조 증거
- [x] AC-3 전환 순간만 강한 노이즈, 기존 피크 로직 미변경: `DreamTransitionController`/
      `CollapseSequence`/`MannequinChargeTrigger` 코드 무변경 확인 + `03b`로 피크 확인
- [x] AC-4 화면을 가리거나 깜빡이지 않음: 전 단계 수치가 1.0(Film Grain 피크, 기존 Story 007
      값 그대로)을 넘지 않고, 신규 단계가 추가한 Vignette 최악값(0.61)도 완전 암전(1.0)에
      크게 못 미침 — 코드 검토로 확인
- [x] AC-5 세 효과 모두 세 단계에서 서로 다른 값: 코드상 9개 값 모두 상이함 확인(위 "코드로만
      확인되는 항목" 참고 — Chromatic Aberration/Vignette의 전환 값은 참고 데이터)

**Reviewed by**: self (solo mode), no independent review
