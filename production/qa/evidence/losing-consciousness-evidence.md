# Test Evidence — Story 002: 자유 이동 중 의식을 잃는 연출

**Story**: `production/epics/depth-5m/story-002-losing-consciousness.md`
**Story Type**: Visual/Feel (BLOCKING — 보관된 스크린샷 + lead sign-off 필요)
**Captured**: 2026-11-05
**Build**: `Builds/Win64/DEPTH5m.exe` (Unity 6000.6.0f1 / URP)

## 연출 요약

씬 시작 후 20초 동안 플레이어는 완전히 자유롭게 이동한다. 그 뒤 **어디에 있든**
의식을 잃는다 — 위치 트리거가 아니다. **카메라는 위치도 회전도 바뀌지 않는다.**
눈꺼풀이 닫히고 시야가 좁아지며 암전되고, `Scene_Game`으로 전환된다.

## 보관 이미지

| 파일 | 증명하는 것 |
|---|---|
| `story-002-losing-consciousness/01-courtyard-front.png` | 중정 정면 — 사방 폐합, 지평선 안 보임, 실제 `prop_flamingo.prefab` |
| `story-002-losing-consciousness/02-courtyard-side.png` | 측면 — 벽·화분·선베드 |
| `story-002-losing-consciousness/03-courtyard-back.png` | 스폰 뒤편 — 벤치·화분으로 채워짐 |
| `story-002-losing-consciousness/04-eyelid-droop.png` | 눈꺼풀 처짐 + 비네트 시작 (조작 아직 살아 있음) |
| `story-002-losing-consciousness/05-eyelid-closing.png` | 거의 감긴 상태 — 카메라는 그대로 수평 |
| `story-002-losing-consciousness/06-black.png` | 완전 암전 |
| `story-002-losing-consciousness/07-next-scene-loaded.png` | `Scene_Game` 복도 로드 완료 |

`stamp.txt`에 캡처 시점의 소스 해시가 들어 있다. 이후 해당 파일이 수정되면 증거가
코드보다 오래된 것으로 드러난다.

## 재현 방법

```
Builds/Win64/DEPTH5m.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 \
  --scene Scene_Intro --afterseconds <초> --screenshot <절대경로> [--yaw <도>]
```

`--afterseconds`는 실시간 기준이다. 프레임 수 기준 `--after`는 이 빌드의 FPS가
1700까지 튀어 초 단위로 정의된 연출의 특정 순간을 잡지 못한다.
`--walkseconds`를 함께 쓰면 그 시간만큼 `--afterseconds`가 뒤로 밀린다.

## 이 증거가 증명하지 못하는 것

- **속도감.** 정지 프레임으로는 20초가 지루한지, 눈꺼풀 1.42초가 적절한지 알 수 없다.
- **20초 동안 입력이 실제로 자유로운지.** 캡처 하네스는 `PlayerController`를 거치지 않고
  `CharacterController`를 직접 움직이므로 입력 경로를 검증하지 않는다.

둘 다 직접 플레이로만 확인된다.

## Sign-off

- [x] Lead sign-off — **Approved** (2026-11-05, `/story-done`에서 승인)

승인 시점에 미검증으로 남은 항목: AC-7 (첫 처짐 구간에서 조작이 유지되는지).
`qa.level: minimal`이라 판정을 막지 않았으며, 승인은 이 점을 알고 내려졌다.
