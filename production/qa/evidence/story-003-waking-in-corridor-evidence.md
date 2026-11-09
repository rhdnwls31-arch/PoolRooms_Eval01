# Test Evidence — Story 003: 실내 수영장 복도에서 눈을 뜨는 연출

**Story**: `production/epics/depth-5m/story-003-waking-in-corridor.md`
**Story Type**: Visual/Feel (BLOCKING — 보관된 스크린샷 + lead sign-off 필요)
**Captured**: 2026-11-05
**Build**: `Builds/Win64/DEPTH5m.exe` (Unity 6000.6.0f1 / URP)

## 연출 요약

Story 002의 암전에서 `Scene_Game`이 로드되면, `GameManager.FirstRound()`가
검은 화면을 유지한 채 라운드를 준비하고 **2.0초에 걸쳐** 밝아진다. 이 2.0초는
이 스토리에서 추가한 `firstFadeTime`이며, 라운드 전환 페이드(0.4초)는
`ScreenFader.fadeTime` 그대로 유지된다 — 눈 뜨는 순간만 느리다.

## AC 대조

| AC | 증거 |
|---|---|
| 1. 암전 후 천천히 밝아짐 | 01 → 02 → 03 의 밝기 단계. `firstFadeTime = 2.0s` |
| 2. 복도 시작 지점 | 01–03 — 스폰 z −15에서 +z를 바라본 복도 |
| 3. 입력 재활성화 | **스크린샷 불가** — 아래 "증명하지 못하는 것" 참조. 직접 플레이로 확인됨 |
| 4. 수심 5m / 정신력 3 | 04 — `DEPTH 5m` 표지판, 정신력 램프 3/3 점등 |

## 보관 이미지

| 파일 | 증명하는 것 |
|---|---|
| `story-003-waking-in-corridor/01-fade-in-mid.png` | t=23.2s — 복도가 어둡게 드러나는 중 |
| `story-003-waking-in-corridor/02-fade-in-complete.png` | t=25.0s — 눈에 띄게 밝아짐 |
| `story-003-waking-in-corridor/03-corridor-settled.png` | t=27.0s — 완전히 밝아짐 |
| `story-003-waking-in-corridor/04-depth-sign-and-lamps.png` | `DEPTH 5m` + 램프 3/3 (1920×1080) |

01–03은 `Scene_Intro`에서 연속 재생해 Story 002 → 003 전환을 그대로 거친 것이다.
04는 표지판을 읽을 수 있게 `Scene_Game`에서 직접 부팅해 제자리에서 180° 돌린 것이다
(`BackTrigger`가 z −18.5에 있어 뒤로 걸어가면 라운드 전환이 걸린다).

`stamp.txt`에 캡처 시점의 소스 해시가 들어 있다. 이후 해당 파일이 수정되면 증거가
코드보다 오래된 것으로 드러난다.

## 재현 방법

```
# 01–03 (연속 재생)
Builds/Win64/DEPTH5m.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 \
  --afterseconds 23.2|25.0|27.0 --screenshot <절대경로>

# 04 (표지판 근접)
Builds/Win64/DEPTH5m.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  --scene Scene_Game --yaw 180 --afterseconds 3.5 --screenshot <절대경로>
```

`--yaw`는 부팅 직후 적용되므로 `Scene_Intro`에서 출발하면 인트로 플레이어가 돌아간다.
`Scene_Game` 플레이어를 돌리려면 `--scene Scene_Game`으로 직접 부팅해야 한다.

## 이 증거가 증명하지 못하는 것

- **AC-3 (입력 재활성화).** 캡처 하네스는 `cc.Move()`로 CharacterController를
  직접 움직여 `PlayerController`를 거치지 않는다. 정지 프레임도 입력을 증명할 수 없다.
  → **사용자 직접 플레이로 확인됨 (2026-11-05)**
- **2.0초가 적절한 길이인지.** 정지 프레임으로는 판단할 수 없다.
  → 사용자 직접 플레이에서 "괜찮다"로 확인됨

## 알려진 인접 이슈 (이 스토리 범위 밖)

`Scene_Intro`의 소품 콜라이더 부재와 수영장 탈출 불가가 이 스토리의 Play 테스트 중
발견되었다. Story 001 영역이며 `docs/tech-debt-register.md` TD-001 / TD-002에 기록됨.
Story 003의 AC 4개에는 영향이 없다.

## Sign-off

- [x] Lead sign-off — **Approved** (2026-11-09, `/story-done`에서 승인, solo mode)
