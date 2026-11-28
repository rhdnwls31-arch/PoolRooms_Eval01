# 다른 컴퓨터에서 이어서 하기 — 사운드 단계 (2026-11-27)

> 이 파일만 읽으면 다른 컴퓨터의 Claude Code가 바로 이어갈 수 있게 정리했다.
> 더 자세한 기록: `production/session-state/active.md` 맨 아래 "사운드" 항목들, `production/qa/evidence/sound-2026-11-27/`.

## 0. 먼저 할 일 (중요)

이 작업은 **아직 커밋·푸시가 안 됐다**(마지막 커밋 `e47df02`). 다른 컴퓨터로 가져가려면 이 컴퓨터에서 먼저 커밋 + `git push`(main) 해야 한다.
- 커밋에서 빼는 것: `.claude/settings.local.json`, `.mcp.json`, `DEPTH-5M-*.md`, `Unity_HDRP_vs_URP_Guide.md`, `docs/DEVELOPMENT-LOG.md`, `weirdcore-door-descent-v2.md`, `production/qa/evidence/doll-room-2026-11-25/09-bed-mid-fade.png`, `Assets/Scenes/Scene_Intro.unity`(줄바꿈만 바뀜), 큰 투어 PNG 폴더.
- 꼭 들어가야 하는 것: `Assets/Audio/**`(원본 `SFX/` + 다듬은 `Game/` + 메뉴 `Menu/Resources/MenuAudio/`, `.meta` 포함), `Assets/Scripts/Audio/**`, `Assets/Scripts/Menu/MenuAudio.cs`, `Assets/Editor/GreyboxGraphicsPass.Audio1127.cs`, `GreyboxGraphicsPass.Feedback1127.cs`, `PlayFromMainMenu.cs`, 수정된 스크립트들, 천장 재질 2개·천장 텍스처 `.meta` 3개, `Scene_DescentGreybox.unity`, `Assets/Tests/EditMode/Editor/GameAudioTests.cs`, `production/qa/evidence/sound-2026-11-27/`.
- 커밋 메시지는 Conventional Commits 한국어, 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. 파일은 경로별로 하나씩 stage.

다른 컴퓨터에서: `git pull` → Unity 6000.6.0f1로 열기 → 아래 3번부터.

## 1. 지켜야 할 규칙 (사용자)

- 답변·질문·버튼 모두 **한국어**.
- 커밋·푸시는 시킬 때만, main에서.
- 저장하는 씬은 **Scene_DescentGreybox만**.
- 고치면 안 되는 파일: GameManager, AnomalyManager, Mannequin*, DreamTransitionController, CollapseSequence, OneWayDoor, PoolReturnDoor, DemoEndTrigger, PlayerController(테스트 때 런타임에 끄는 건 괜찮음).
- 증거는 `production/qa/evidence/`, 단계마다 `production/session-state/active.md`에 덧붙이기.
- 스크린샷은 적게, 숫자로 확인. 사용자는 깜빡임을 싫어함.
- 내려받기는 파일 이름·출처·크기를 말하고 허락받기. 로그인·비밀번호는 사용자만.
- 전체 순서: 빌드 정리 → 성능 → 메뉴 → **사운드(지금)** → 외부 플레이테스트 → 최종 테스트.

## 2. 지금까지 만든 소리 구조

| 파일 | 하는 일 |
|---|---|
| `Assets/Scripts/Audio/GameAudio.cs` | 씬에 하나. `GameAudio.Play(Cue, 위치)` 한 번 나는 소리, `Stop`, `StartLoop/StopLoop`(반복), `PlayMusic/StopMusic`(주제곡), 방 환경음 두 칸 교차. 새로 시작해 처음 수영장이면 주제곡 25초 → 10초 줄임 |
| `AudioZone.cs` | 방 범위마다 환경음·크기·바닥 종류(Tile/Carpet/Wood/Stone/Wet). `silenceWhenRevealed`(샤워실), `onlyWhileActive`(가라앉은 도시: 물 차오를 때만), `introMusic` |
| `FootstepAudio.cs` | 발소리. 위치 변화로 속도 계산(멈추면 소리 안 남). 걸음 거리 걷기 1.45m·뛰기 2.05m. 물속이면 젖은 소리. 떨어질 때 바람 |
| `WaterAudio.cs` | 발이 0.25m 넘게 잠기면 첨벙, 카메라가 물속이면 소리 먹먹(900Hz) |
| `AudioCraft.cs` | 에디터에서 소리 다듬기(앞 무음 자르기, 크기 맞추기, 반복 이음매, 걸음 자르기, `Drive` 눌러 키우기) |
| `Assets/Scripts/Menu/MenuAudio.cs` | 메인 메뉴: 주제곡 + 영상별 환경음(flamingo=amb_outdoor_pool, poolhall=amb_pool, city=amb_city) + 브라운관 소리, "시작하기" 가라앉는 소리. **버튼 마우스 올림·누름 소리는 사용자 결정으로 뺌** |
| `Assets/Editor/GreyboxGraphicsPass.Audio1127.cs` | 메뉴 **DEPTH 5m > Audio > Apply Sound**: `Assets/Audio/SFX` 원본을 다듬어 `Assets/Audio/Game`·`Menu/Resources/MenuAudio`에 쓰고 씬에 연결·저장. 원본은 이름 앞부분(괄호 앞)으로 찾음 |
| `GreyboxGraphicsPass.Feedback1127.cs` | 먼 방 그리기(입구 벽·수영장 홀 90m), 천장 매끈함·반사 끔, 카메라 TAA, 천장 텍스처 Kaiser, 거실 TV hideDelay 0 |
| `Assets/Editor/PlayFromMainMenu.cs` | Play를 누르면 메인 메뉴부터. 메뉴 **DEPTH 5m > Play From Main Menu**는 누를 때마다 켜짐/꺼짐이 바뀜(한 번 눌러 꺼진 적 있음) |

게임 코드 훅: GazeTarget(찾음·힌트, `foundSound` 마지막 마네킹은 끔), RevealWhenExplored(출구 열림), DollStepper(인형 이동, 다시 보면 소리 멈춤, 문 삐걱), BlueRoomDoor(복도 들어서면 노크 한 번, 도어뷰어 인외 소리, 문 삐걱), SealBehindPassage(벽으로 막힐 때 쾅), WanderCursor(클릭), FallRespawn, HiddenEnding(VHS 채널 지직·테이프 소리·주제곡), LightFlicker. 순간이동 전환 소리는 끔(물 없는 방에서 물거품처럼 들렸음).

## 3. 남은 일

1. **배경음악 다시 고르기** — 사용자: 처음 "동동동동" 울림은 좋은데 뒤로 갈수록 드럼이 나옴 → 드럼 없는 몽환적 울림만.
   - VARCO 음악(https://sound.varco.ai/app/music, 200P)에서 드럼 없는 프롬프트로 다시 생성 중이었다(결과 확인 전). 프롬프트: "Pure dreamy ambient drone, no drums, no percussion, no beat ... Slow soft pulsing synth notes echoing in a huge empty indoor pool ..."
   - 사용자가 고른 곡을 `Assets/Audio/SFX/menu_theme(...).wav`(또는 mp3) 이름으로 넣으면 Apply Sound가 연결(메뉴·인트로 처음·엔딩 같은 곡).
   - 대안: 마음에 드는 곡의 드럼 나오기 전 앞부분만 잘라 반복하기(코드로 가능).
2. **파란방 소리 키우기** — 이미 코드 반영(`bedroom_night` drive 5, 범위 크기 0.7). **Apply Sound를 아직 안 돌렸다** → 돌리고 Play로 확인.
3. **메뉴 버튼 소리 빼기** — 코드 반영 완료(MenuAudio, MenuController). 컴파일·Play 확인 전.
4. 이번에 들어온 파일 연결(Apply Sound 한 번이면 됨): `stone_slam`, `bedroom_night`, `pool_step`, `door_creak`, `falling_wind`, `vhs_loop`, `crt_whine`, 그리고 이름을 바꿔 둔 `outdoor_pool(GEN_3)`, `tv_switch(GEN_3)`, `menu_start(GEN_1)`.
5. Apply 뒤 EditMode 테스트(마지막 47/47) 다시 돌리기, Play로 숫자 확인, 증거·active.md 기록.
6. 사용자가 직접 들어보고 크기 피드백 → 조정.
7. 아직 화면으로 확인 못 한 것: 천장 깨짐(TAA로 바꿈 — 움직이는 것 뒤 잔상 생기는지), TV가 밝은 화면으로 바뀌는지.
8. 열린 문제: 목각인형 방이 `RoomRoute.RoomNames`에 없어 이어하기 저장이 거실/침실로 묶일 수 있음.
9. 그다음: 외부 플레이테스트 → 최종 테스트.
10. **물에 들어갈 때 첨벙 소리 — 끔(2026-11-28 사용자: 경박하다).** `WaterAudio.playEnterSplash = false`. 물 헤치는 발소리·물속 먹먹함은 그대로.

## 3-1. 방 소리 다시 고르기 — 사용자 의견(2026-11-28, 아직 결정 전)

사용자가 직접 플레이해 보고 남긴 의견. **둘 다 사용자가 아직 고민 중이라 소리를 바꾸지 않았다.** 방향을 정해 주면 VARCO에서 뽑거나 코드로 만든다.

### 꽃무늬 복도 (`AudioZone` "Corridor", 지금 `amb_corridor` = `fluorescent_hum`, 크기 0.18)

- 사용자: "노이즈 소리보다 다른 거면 좋겠는데 아직 고민."
- 지금 소리는 형광등 웅웅거림인데 노이즈처럼 들린다. 금 간 복도(`Crack`)도 같은 소리를 쓰므로, 바꿀 때 금 간 복도를 따로 둘지도 정해야 한다.
- 후보(Claude 제안):
  1. **멀리서 들리는 오래된 라디오·오르골** — 벽 너머 다른 방에서 새어 나오는 듯 작고 먹먹하게(저역 통과). 노란 방 오르골(`music_box`)과 겹치지 않게 다른 곡조로.
  2. **거의 무음 + 가끔 벽 너머 발소리** — 기본은 아주 작은 방 울림, 수십 초에 한 번 벽 너머에서 누가 걷는 소리(위치를 옮겨 가며). 고리 복도(`LoopCorridor`)와 잘 어울린다.
  3. **낡은 집의 나무 삐걱임·벽지 바스락** — 꽃무늬 벽지·나무 바닥에 맞춘 실내 소리.
  4. 형광등 소리를 유지하되 아주 낮은 음(60Hz 근처)만 남기고 지지직거리는 높은 소리를 걸러 "노이즈 느낌"만 없애기(코드로 가능, 새 소리 필요 없음).

### 파란 방 (`AudioZone` "Bedroom"·"BlueCorridor", 지금 `amb_blue` = `bedroom_night`, drive 5, 크기 0.7)

- 사용자: "물에 잠기는 듯한 소리여도 괜찮을 것 같기도. 아니면 다른 소리…"
- 지금은 밤 침실 소리를 크게 눌러 키운 것(예전에 안 들린다는 피드백 때문).
- 후보(Claude 제안):
  1. **물에 잠긴 방** — 방에 들어서면 귀에 들리는 소리 전체를 물속처럼 먹먹하게(`WaterAudio`의 물속 필터와 같은 방식, 약 900Hz 저역 통과) + 낮게 웅웅 울리는 물 소리 반복 + 가끔 물방울·거품 소리. 물이 없는데 물속처럼 들리는 어긋남이 파란 조명과 맞는다. 코드로 거의 다 만들 수 있고 물 울림 반복 소리 하나만 새로 뽑으면 된다.
  2. **멀리서 들리는 수영장 소리를 물속에서 듣는 것처럼** — 첫 수영장 환경음(`amb_pool`)을 먹먹하게 깔아 "처음 장소의 기억" 느낌.
  3. 지금 밤 침실 소리 유지 + 크기만 조정.
- 노크(복도 들어설 때 한 번)·도어뷰어 소리·문 삐걱은 어느 쪽이든 그대로 둔다.

## 4. VARCO Sound 쓰는 법

- 크롬(Claude in Chrome)으로 사용자가 로그인해 둔 https://sound.varco.ai/app/sfx (효과음, 50P/회, 3개씩, 최대 약 10초) / `/app/music` (200P, 2곡, 2~3분).
- 프롬프트 칸 클릭 → ctrl+a, Delete → 입력 → 생성 버튼(화면 크기에 따라 위치가 바뀜, 스크린샷으로 확인). 15~17초 기다렸다 다음.
- VARCO가 파일 이름을 스스로 붙인다(예: `stone_slam(GEN_1)`). 기존 이름과 겹치면 넣을 때 이름을 바꿔야 함.
- 사용자가 듣고 골라 `Assets/Audio/SFX`에 직접 넣는다. 잔여 포인트 약 96,300P.

## 5. 확인 명령 (Unity MCP)

- 소리 연결: `System.Type.GetType("GreyboxGraphicsPass, Assembly-CSharp-Editor").GetMethod("ApplyAudioMenu").Invoke(null,null)`
- 테스트: TestRunnerApi + 콜백으로 `Temp/gaze-tests.txt`에 결과 쓰기(EditMode).
- Enter Play Mode Options에서 도메인 다시 불러오기가 꺼져 있어 static 값은 `RuntimeInitializeOnLoadMethod`로 초기화한다.
