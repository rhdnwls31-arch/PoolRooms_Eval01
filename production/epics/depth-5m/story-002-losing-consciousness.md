# Story 002: 자유 이동 중 의식을 잃는 연출

> *2026-11-05: "가장자리에서 떨어진다"는 초기 설계는 **폐기**되었다. 파일명과 증거 폴더도
> `story-002-losing-consciousness`로 바꿨고, `design/game-brief.md`와 `EPIC.md`에서도
> 해당 표현을 전부 제거했다.*

> **Epic**: DEPTH 5m
> **Status**: Complete
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: S
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-05

## Context

**GDD**: `design/game-brief.md`
**Requirement**: Brief MVP feature 2 — 자유 이동 중 의식을 잃는 연출

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

*2026-11-05 3차 개정: 카메라를 수직으로 내리는 방식은 폐기했다. 몸이 접히는 단서가
없으니 "사람이 쓰러진다"가 아니라 **"바닥이 꺼진다 / 엘리베이터가 내려간다"**로 읽혔다.
숫자 조정으로 고쳐지는 문제가 아니라 표현 방식의 문제였다. 의식을 잃는 일은 원래
눈에서 일어나므로 **눈꺼풀이 감기고 시야가 좁아지는** 방식으로 바꿨다 —
카메라는 전혀 움직이지 않는다.*

- [ ] 씬 시작 후 약 20초 동안 플레이어는 이동·시점 **둘 다 완전히 자유**다
- [ ] 발동은 **특정 위치에 닿아서가 아니라 시간이 되면** 어디에 있든 시작된다
- [ ] **연출 중 카메라는 위치도 회전도 바뀌지 않는다** (하강·틸트·롤 전부 없음)
- [ ] 위/아래 검은 막이 눈꺼풀처럼 닫힌다 — 윗막이 더 많이 내려온다
- [ ] 처짐 → 살짝 떠짐 → 완전히 감김 순서로, 한 번 버티는 동작이 들어간다
- [ ] 눈꺼풀과 함께 **시야가 좁아진다** (URP Vignette, 런타임 생성)
- [ ] 첫 처짐 구간에서는 **아직 조작이 살아 있다** — "어? 왜 이러지"를 느낄 시간
- [ ] 완전히 감긴 뒤 조작이 차단되고 화면이 **검정**이 된다
- [ ] 쓰러지는 소리가 재생된다
- [ ] 연출이 끝나면 다음 씬(`Scene_Game`)으로 전환된다

---

## Implementation Notes

- 트리거 감지는 쓰지 않는다. `CollapseSequence.Start()`가 `freeRoamTime`만큼 기다렸다가
  발동한다 — 플레이어가 타이머의 존재를 모르는 채 평소대로 걷고 있어야 "갑자기 쓰러진다"가 된다.
- 기존 `ScreenFader.cs`를 재사용한다 — 새 페이드 시스템을 만들지 않는다. 물빛은 `SetColor()`,
  0.7초 연출에 맞는 짧은 페이드는 `FadeOut(duration)` 오버로드로 처리한다.
- 입력 차단은 `PlayerController.enabled = false` 한 줄 (`GameManager.SetPlayerControl`과 같은 방식).
  자유 이동 구간에는 아무것도 잠그지 않으므로 `PlayerController`는 **수정하지 않는다**.
- 하강은 **카메라의 `localPosition.y`만** 내린다. Player 루트와 CharacterController는 건드리지
  않으므로 물리와 충돌하지 않는다.
- 하강 깊이는 바닥 높이까지만(1.3m). 수면 아래까지 내리면 데크 한가운데서 쓰러졌을 때
  바닥을 뚫고 내려가 버린다 — 위치 트리거를 없앤 이상 어디서든 쓰러질 수 있기 때문이다.
- 가속 곡선 지수는 1.5~1.7 선. 더 키우면(u², u^2.2) 앞 절반에서 카메라가 거의 움직이지 않아
  물빛 페이드가 정작 떨어지는 동작을 덮어 버린다 — 실행에서 관찰하고 고친 사항이다.
- `fadeDuration`은 `collapseTime`보다 충분히 짧아야 한다(0.25 vs 0.7). 같은 이유다.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: 도입부 환경/분위기 구성
- [Story 003]: 실내 복도에서 눈을 뜨는 연출 — 이 스토리는 `Scene_Game`을 **로드하는 데까지**이고, 로드 이후의 연출은 Story 003이 맡는다

---

## Files

- Assets/Scripts/CollapseSequence.cs (연출 전체. 구 `PoolEdgeTrigger.cs`에서 개명 — `.meta` GUID 유지)
- Assets/Scripts/CollapseSequence.cs.meta (GUID `bacbe2d3…` 동일)
- Assets/Editor/IntroMapBuilder.cs (new — 도입부 맵/눈꺼풀/컨트롤러를 코드로 생성)
- Assets/Scripts/IntroFadeIn.cs (+.meta)
- Assets/Scripts/ScreenFader.cs (modified — `SetColor()` + `FadeOut(duration)`)
- Assets/Scripts/ScreenshotOnArg.cs (modified — `--afterseconds`, `--yaw`. QA 하네스)
- Assets/Scenes/Scene_Intro.unity (IntroMapBuilder가 생성)
- Assets/Materials/Env/Env_Court_*.mat (new ×7 — 야외 전용. 실내 타일을 쓰면 중정이 차갑게 보인다)
- (unchanged) Assets/Scripts/PlayerController.cs


## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above.*

**[Visual/Feel — manual verification steps]:**

- **AC 전체**: 쓰러지는 연출
  - Setup: `Builds/Win64/DEPTH5m.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --scene Scene_Intro`
  - Verify: 25초간 자유롭게 걷고 둘러봐진다 → 갑자기 입력이 끊기며 0.7초 만에 주저앉는다
    → 물빛 → `Scene_Game`
  - Pass condition: 25초 동안 **어떤 입력도 막히지 않고**, 쓰러질 때 수평선이 대각선으로
    기울지 않으며, 검은 화면이 나타나지 않음
  - 자동 캡처: 위 커맨드에 `--afterseconds <초> --screenshot <abs path>` 추가.
    `--walkseconds`를 함께 쓰면 그 시간만큼 `--afterseconds`가 뒤로 밀리므로 빼고 계산할 것.

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: a retained screenshot in `production/qa/evidence/` + sign-off in `production/qa/evidence/losing-consciousness-evidence.md`

**Status**: [x] 스크린샷 보관 완료 (2026-11-05, 3차) — lead sign-off 대기 중

`production/qa/evidence/story-002-losing-consciousness/` (해시 스탬프: `stamp.txt`)

| 파일 | 증명하는 것 |
|---|---|
| `01-courtyard-front.png` | 중정 정면 — 사방 폐합, 지평선 안 보임, 실제 플라밍고 프리팹 |
| `02-courtyard-side.png` | 측면 — 벽·화분·선베드 |
| `03-courtyard-back.png` | 스폰 뒤편 — 벤치·화분으로 채워짐 |
| `04-eyelid-droop.png` | 눈꺼풀 처짐 + 비네트 시작 (조작은 아직 살아 있음) |
| `05-eyelid-closing.png` | 거의 감긴 상태 — 카메라는 그대로 수평 |
| `06-black.png` | 완전 암전 |
| `07-next-scene-loaded.png` | `Scene_Game` 복도 로드 완료 |

> 정지 프레임은 **속도감과 "20초 동안 입력이 자유로운지"를 증명하지 못한다.**
> 그 둘은 직접 플레이로만 확인된다.

---

## Dependencies

- Depends on: `production/epics/depth-5m/story-001-night-pool-intro.md`
- Unlocks: `production/epics/depth-5m/story-003-waking-in-corridor.md`

---

## Completion Notes

**Completed**: 2026-11-05
**Criteria**: 9/10 passing
**Untested criteria**: AC-7 (첫 처짐 구간에서 조작이 아직 살아 있는지) — 직접 플레이로 미확인.
`qa.level: minimal`에서는 미검증 항목이 판정을 막지 않는다. 코드상으로는
`player.enabled = false`가 첫 `Lids()` 호출이 끝난 뒤에 실행되므로 보장되지만,
실행으로 확인된 것은 아니다.

**Deviations** (둘 다 ADVISORY — tech debt로 등록하지 않음, 사용자 판단으로 수용):

1. `collapseSound.Play()`는 호출되지만 오디오 클립이 아직 할당되지 않아 **무음**으로
   재생된다. 이 프로젝트의 플레이스홀더 오디오 방침(VARCO Sound로 나중 교체)과 일치하며,
   `AudioSource`를 별도 컴포넌트로 둬 나중에 소리만 갈아 끼울 수 있게 되어 있다.
2. 이 문서의 `## QA Test Cases` 섹션이 **구 설계(카메라 틸트)를 기술한 채 남아 있다** —
   "25초", "0.7초 만에 주저앉는다", "수평선이 대각선으로 기울지 않으며". AC 섹션은 3차
   개정으로 눈꺼풀 방식이 되었으나 이 섹션만 갱신되지 않았다. 동작에는 영향이 없고
   AC와 모순되지도 않지만, 다음에 이 문서를 읽는 사람이 헷갈릴 수 있다.

**Test Evidence**: `production/qa/evidence/story-002-losing-consciousness/` (01~07, 7장) +
sign-off `production/qa/evidence/losing-consciousness-evidence.md`
증거 최신성: `stamp.txt` 대조 시 `IntroMapBuilder.cs`와 `Scene_Intro.unity`가 캡처 이후
변경됨 — 플라밍고 위치·회전 보정이며 이 스토리의 AC와는 무관하다.

**Reviewed by**: self, no independent review (solo mode)
QL-TEST-COVERAGE skipped — Solo mode / LP-CODE-REVIEW skipped — Solo mode
