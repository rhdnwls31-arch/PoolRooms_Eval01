## Technical Debt Register
Last updated: 2026-11-09
Total items: 2 | Estimated total effort: S + S

| ID | Category | Description | Files | Effort | Impact | Priority | Status | Added | Sprint |
|----|----------|-------------|-------|--------|--------|----------|--------|-------|--------|
| TD-001 | Architecture | Scene_Intro 소품에 콜라이더가 전혀 없어 플레이어가 벤치·선베드·파라솔·화분을 모두 통과한다. 원인은 누락이 아니라 `Box(collide:false)` / `Cyl()`의 전부-아니면-전무 헬퍼 설계 — 하위 박스마다 콜라이더를 켜면 CharacterController가 의자 다리에 끼는 문제가 있어 통째로 껐다. 해법은 그룹당 단순화된 BoxCollider 1개. | Assets/Editor/IntroMapBuilder.cs | S | Med | — | Open | 2026-11-09 | Backlog |
| TD-002 | Architecture | `Prop_PoolLadder_W/E`가 데크 위 손잡이 기둥(y 0~0.9)일 뿐 수영장 안으로 내려가지 않아, 수영장에 들어간 플레이어가 20초 뒤 CollapseSequence가 발동할 때까지 나올 수 없다. **설계 의도(2026-11-05 확정): 수영장에 들어가는 것은 허용하고, 사다리로 올라올 수 있게 한다** — 코핑 턱을 높여 진입을 막는 방향이 아니다. | Assets/Editor/IntroMapBuilder.cs | S | High | — | Open | 2026-11-09 | Backlog |

### 상세 — 재조사 없이 바로 착수할 수 있도록

발견 경위: Story 003의 Play 테스트 중 사용자가 발견. Story 001(도입부 공간) 영역이며
Story 003의 AC 4개에는 영향이 없다.

측정된 기준값: `CharacterController` height 1.8 / radius 0.3 / **stepOffset 0.3** / skinWidth 0.08,
`PoolDepth 1.5` (바닥 윗면 y −1.5), `PoolHalfX 4.3` / `PoolHalfZ 7.3`, `CurbHeight 0.36`, 데크 y 0.

**TD-001 — 그룹당 콜라이더 1개** (렌더러 없는 자식에 BoxCollider만)

| 소품 | center | size | 비고 |
|---|---|---|---|
| Lounger | (0, 0.35, 0) | (0.80, 0.70, 2.00) | 다리·등받이 통째로 |
| Bench | (0, 0.50, −0.05) | (2.20, 1.00, 0.70) | |
| SideTable | (0, 0.26, 0) | (0.55, 0.52, 0.55) | |
| Parasol | (0, 1.10, 0) | (0.22, 2.20, 0.22) | 기둥만 — 캐노피(y 2.2)는 키 1.8 위라 통과 |
| Planter | (0, 0.30, 0) | (0.76, 0.60, 0.76) | 화분만 — 잎은 통과(스치는 느낌 유지) |
| TowelStack / Flamingo / Radio | 없음 | | 선베드 위·물 위·테이블 위 |

단일 상자라 틈이 없어, 원래 주석이 경고한 "의자 다리 끼임"이 재발하지 않는다.

**TD-002 — 사다리를 바닥까지 연장 + 보이지 않는 발판**

- 위치: `z = −7.4`(코핑 위) → **`z = −7.05`**(수영장 개구부 안쪽), x는 기존 ±2.2 유지
- 레일: y `−1.45 ~ +0.95` (cylinder scale.y 1.2, center y −0.25)
- 가로대: y `−1.25, −1.00, −0.75, −0.50, −0.25` + 기존 손잡이 `0.88`
- **발판 콜라이더**(비가시): 가로대마다 center (0, y−0.06, 0.18) size (0.50, 0.12, 0.36),
  맨 위는 y `0.00`으로 데크와 수평. 단 높이 **0.25 < stepOffset 0.3** 이라
  CharacterController가 자동으로 올라간다 — **코드 수정·새 입력 0건**
- **코핑 북쪽에 폭 1.0m 개구부 2곳** (x ±2.2). 없으면 올라와도 0.36 턱에 막힌다.
  현재 `PoolCurb_North`(x −4.6~4.6)를 3구간으로 분할: `[−4.6,−2.7] [−1.7,1.7] [2.7,4.6]`

**진입에 대한 메모**: 코핑 0.36 vs stepOffset 0.3 + skinWidth 0.08 → 여유 0.06m라
달리면 턱을 넘어 들어가진다. **이건 이제 의도된 동작이므로 고치지 않는다.**

적용은 `IntroMapBuilder.cs` 수정 후 Unity 메뉴 `DEPTH 5m > Rebuild Intro Map`.
이 빌더는 `Environment`·`Dummies` 하위를 지우고 재생성하므로, 돌리기 전에
에디터에서 손으로 조정한 소품이 있는지 확인할 것.
