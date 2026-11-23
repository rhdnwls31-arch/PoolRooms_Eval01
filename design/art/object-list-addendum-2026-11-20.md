# DEPTH — 추가 방 오브젝트 목록 (2026-11-20)

> `design/art/object-list-and-prompt-guide.md`(이하 **원래 문서**)의 첫 버전(2026-11-18) 뒤에 생긴 방의 오브젝트만 모았다.
> 아트 스타일(§2-1~2-3), 프롬프트 틀(§2-4), 납품 규칙(§2-5)은 원래 문서를 그대로 따른다.
> 아래 프롬프트는 원래 문서 §2-4 틀에 이 물건의 내용을 채운 것이라 그대로 붙여 넣으면 된다.

**넣은 기준**
- 바닥에 놓이거나 공중에 떠 있어서 **3D 모델이 있어야** 보기 좋은 것만 적었다.
- 벽에 붙는 것(문, 창, 벽등, 형광등), 그림 한 장(텍스처)으로 되는 것(웅덩이 반사, 배수구, 벽지, 카펫), 구조물(계단, 산책로, 난간, 채광창)은 뺐다. 빼 둔 것은 맨 아래 §4에 이유와 함께 적었다.
- 이미 VARCO 모델로 바꾼 것은 적지 않았다.

---

## 1. 꽃무늬 아파트 복도 (노란 방 → 복도 → 밤의 거실)
색감: 바랜 분홍·크림 꽃무늬 벽지, 짙은 나무 징두리, 갈색 카펫, 푸르스름한 형광등. 끝만 따뜻한 주황 불빛과 노란 풍선.
똑같은 현관문이 끝없이 이어지는 복도라, 바닥에 놓이는 물건은 거의 없다. 끝에서 기다리는 풍선 하나가 이 방의 표지다.

| 오브젝트 | 설명 | 크기(m) | 우선 | 더미 경로 |
|---|---|---|---|---|
| 스마일 풍선 | 노란 둥근 풍선. 검은 점 눈 두 개와 웃는 입(선 하나), 아래로 늘어진 가는 줄 | 풍선 지름 0.6, 줄 0.9 (전체 높이 1.5) | 찾기 | `GB_02c_Corridor/GFX_Generated/End/SmileBalloon` |

**스마일 풍선**
- 원점: **줄 끝**(바닥 쪽). 풍선이 떠서 흔들릴 때 줄 끝을 축으로 돈다.
- 앞면(+Z)에 얼굴이 오게 한다.
- 웅덩이에 비치는 반사 그림은 이 모델을 Unity에서 위에서 찍어 만든다. 따로 그리지 않는다.

```
A single yellow round party balloon with a simple smiley face (two black dot eyes, one thin curved smile line),
a thin limp string hanging below, dreamcore / liminal space aesthetic,
clean simplified realistic style, slightly toy-like proportions, soft rounded shape,
matte latex material, muted faded yellow and black,
1990s everyday object, no text, no logos, no brand marks,
isolated on a plain light grey background, full object visible including the whole string, centered,
3/4 front view from slightly above, soft even studio lighting, no harsh shadows,
no other objects, no people, product render, high detail silhouette
```

---

## 2. 가라앉은 집들의 도시 (컴퓨터 속 → 도시 → 천사 방)
색감: 옅은 하늘색·흰색·민트색 수영장 타일, 흰 난간, 빨강·흰 구명 튜브. 물이 차면 전체가 청록빛.
맑고 조용한데 너무 크다. 60×60m 실내에 타일 집이 탑처럼 쌓여 있다.

| 오브젝트 | 설명 | 크기(m) | 우선 | 더미 경로 |
|---|---|---|---|---|
| 구명 튜브 | 빨강·흰 4칸 줄무늬 구명 튜브. 물이 차오르면 떠오른다 | 지름 0.8, 두께 0.2 | A | `.../Rings/LifeRing` |
| 감시탑 의자 | 수영장 감시원용 흰 높은 의자. 다리 네 개, 다리 사이 발판, 위에 좌석과 등받이 | 1.6 × 3.5 × 1.6 | B | `.../Props/LifeguardChair` |
| 다이빙대 | 파란 판과 흰 받침 | 판 4.2 × 0.1 × 0.7, 받침 0.6 × 0.2 × 0.9 | B | `.../Walkway/DivingBoard`, `DivingBoardBase` |

**구명 튜브**
- 원점: **몸통 가운데**(물에 떠 있는 물건).
- 눕혀 놓은 것과 세워 둔 것 모두 같은 모델을 돌려서 쓴다.

```
A single round lifebuoy ring with four alternating red and white segments, dreamcore / liminal space aesthetic,
clean simplified realistic style, slightly toy-like proportions, soft rounded shape,
matte plastic foam material, muted faded red and white,
1990s public swimming pool object, no text, no logos, no brand marks, no rope,
isolated on a plain light grey background, full object visible, centered,
3/4 front view from slightly above, soft even studio lighting, no harsh shadows,
no other objects, no people, product render, high detail silhouette
```

**감시탑 의자**
- 원점: 바닥 가운데. 앞면(+Z)은 좌석이 향하는 쪽.

```
A single tall white lifeguard chair: four straight legs, horizontal footrest rungs between the legs,
a small seat with a low backrest on top, dreamcore / liminal space aesthetic,
clean simplified realistic style, slightly toy-like proportions, soft rounded bevels,
matte painted metal material, muted faded white with light grey,
1990s public swimming pool object, no text, no logos, no brand marks,
isolated on a plain light grey background, full object visible, centered,
3/4 front view from slightly above, soft even studio lighting, no harsh shadows,
no other objects, no people, product render, high detail silhouette
```

**다이빙대**
- 원점: 받침 바닥 가운데. 판은 +Z 쪽으로 뻗는다.

```
A single swimming pool diving board: a long flat pale blue board resting on a short white base,
dreamcore / liminal space aesthetic, clean simplified realistic style, slightly toy-like proportions, soft rounded bevels,
matte fiberglass and painted metal materials, muted faded pale blue and white,
1990s public swimming pool object, no text, no logos, no brand marks,
isolated on a plain light grey background, full object visible, centered,
3/4 side view from slightly above, soft even studio lighting, no harsh shadows,
no other objects, no people, product render, high detail silhouette
```

---

## 3. 원래 문서 모델을 그대로 다시 쓰는 것 (새로 뽑지 않아도 됨)
| 쓰이는 곳 | 다시 쓰는 모델 |
|---|---|
| 도시의 수영장 사다리(높이 4~9m) | 원래 문서 §3-0 인트로 **수영장 사다리**. 긴 것은 Unity에서 발판 부분을 반복해 늘린다 |
| 도시 창밖으로 나오는 검은 인외 | 파란 복도 **도어뷰어 속 인외**(VARCO, 완료). 검은 재질과 고개 돌림만 Unity에서 바꿨다 |
| 노란 방 키다리 삐에로(2.7m, 풍선을 놓아 출구로 날림) | `VAR_Yellow_Pierrot_Balloon`(VARCO, 완료). 원래 문서의 "앉은 인형 0.6m, 열쇠" 설정은 이 모델로 바뀌었다 |

---

## 4. 뺀 것과 이유
| 방 | 오브젝트 | 뺀 이유 |
|---|---|---|
| 꽃무늬 복도 | 현관문(반복), 마지막 현관문 | 벽에 붙는 문. 지금 상자 문짝으로 충분하다 |
| 꽃무늬 복도 | 끝 문 위 벽등, 천장 형광등 | 벽·천장에 붙는 등. 형광등은 원래 문서 §3-10 공용 |
| 꽃무늬 복도 | 웅덩이 속 풍선 반사, 꽃무늬 벽지, 카펫, 징두리 | 텍스처나 재질로 된다 |
| 도시 | 쌓인 타일 집(3~4종) | 집 한 채가 8~12m인 건물 크기 구조물. 지금 Unity 상자 집에 타일 재질을 입혀 쓴다(2026-11-20 뺌) |
| 도시 | 물 찬 집(출구), 창(상자 창) | 찾기 출구로 문 열림·안쪽 빛·물 연출이 Unity에 맞춰져 있어 지금 상자 집을 유지한다(2026-11-20 뺌) |
| 도시 | 배수구 | 바닥 텍스처로 된다 |
| 도시 | 계단, 산책로, 난간, 기둥, 채광창 | 구조물. 지금 형태를 유지하고 재질만 바꾼다 |
| 도시 → 천사 방 | 천사 방에 잠깐 남는 문틀 | 1~2초 보였다 사라져서 지금 상자 문틀로 충분하다(원래 문서 §3-7b B) |

---

## 5. 만드는 순서 추천
1. **스마일 풍선**: 찾기 규칙과 연결돼 플레이 감각에 바로 영향이 있다.
2. **구명 튜브**: 도시 곳곳에 여러 번 쓰인다.
3. **감시탑 의자**, **다이빙대**.

---

## 6. 방 채우기 소품 (2026-11-20 추가)
넓은 방이 휑해서 노란 방·밤의 거실·파란 침실에 더미 소품을 3~5개씩 더 놓았다. 모두 벽 쪽에 모여 있다.
2026-11-23: 노란 방 FilingCabinet·WaterCooler·OfficeCouch·CardboardBoxes는 VARCO로 바꿈.
VARCO FBX를 `Assets/VARCO/VAR_<방>_<이름>.fbx`로 올리면 같은 자리의 더미와 바꾼다(코드는 `FillProp`, 프리팹이 있으면 프리팹, 없으면 더미).
원점은 모두 **바닥 가운데**, 앞면 **+Z**. 우선순위는 모두 B(배경). 파란 침실 것은 Unity에서 파랗게 칠하므로 옅은 회청색으로 뽑는다.

| 방 | 파일 이름 | 설명 | 크기(m, 폭×높이×깊이) |
|---|---|---|---|
| 노란 방 | `VAR_Yellow_FilingCabinet` | 4단 철제 서류함(회색). 2개 놓는다 | 0.5 × 1.32 × 0.6 |
| 노란 방 | `VAR_Yellow_WaterCooler` | 사무실 정수기, 위에 파란 물통 | 0.34 × 1.35 × 0.34 |
| 노란 방 | `VAR_Yellow_OfficeCouch` | 낡은 2~3인용 대기실 소파, 한쪽 쿠션이 꺼짐 | 1.9 × 0.85 × 0.85 |
| 노란 방 | ~~`VAR_Yellow_StackedChairs`~~ | 쌓아 둔 회의실 의자 5개 — **뺌(2026-11-23)**: 겹친 의자는 VARCO 텍스처를 고칠 곳이 너무 많아 Unity 더미 그대로 둔다 | 0.48 × 1.25 × 0.5 |
| 노란 방 | `VAR_Yellow_CardboardBoxes` | 테이프 붙은 골판지 상자 3개 더미 | 1.15 × 0.75 × 0.55 |
| 밤의 거실 | `VAR_LivingRoom_Sideboard` | 낮은 나무 장식장(문 2짝), 위에 꽃병과 탁상시계 | 1.6 × 0.8 × 0.45 |
| 밤의 거실 | `VAR_LivingRoom_Bookcase` | 책이 꽂힌 나무 책장(5단) | 0.9 × 1.9 × 0.34 |
| 밤의 거실 | `VAR_LivingRoom_SideTable` | 네 다리 작은 협탁, 위에 재떨이 | 0.45 × 0.55 × 0.45 |
| 밤의 거실 | `VAR_LivingRoom_Armchair` | 천 씌운 1인용 안락의자(소파와 같은 갈색) | 0.85 × 0.95 × 0.85 |
| 밤의 거실 | `VAR_LivingRoom_PottedPlant` | 큰 잎 화분(고무나무 같은) | 0.4 × 1.3 × 0.4 |
| 파란 침실 | `VAR_Bedroom_Wardrobe` | 문 2짝 옷장 | 1.2 × 2.0 × 0.6 |
| 파란 침실 | `VAR_Bedroom_Nightstand` | 서랍 하나 협탁(스탠드는 따로 둠, 모델에 넣지 않는다) | 0.45 × 0.55 × 0.4 |
| 파란 침실 | `VAR_Bedroom_StandingMirror` | 받침대가 뒤로 기댄 전신 거울 | 0.55 × 1.6 × 0.3 |
| 파란 침실 | `VAR_Bedroom_ToyChest` | 뚜껑 달린 나무 장난감 상자, 옆에 공 하나 | 0.9 × 0.5 × 0.45 |

프롬프트는 §2-4 틀의 `[ ]` 세 곳만 바꿔 넣는다.

| 파일 이름 | [오브젝트 영문 설명] | [재질] | [색] |
|---|---|---|---|
| FilingCabinet | four-drawer steel office filing cabinet | painted metal | faded grey, dark grey handles |
| WaterCooler | office water cooler with a large blue water bottle on top | plastic | off-white, pale blue |
| OfficeCouch | worn two-seat waiting-room couch, one cushion sagging | fabric, wood feet | faded olive, brown |
| StackedChairs | stack of five identical plastic meeting chairs with metal legs | plastic, metal | faded grey, silver |
| CardboardBoxes | pile of three taped cardboard moving boxes | cardboard | faded brown, beige tape |
| Sideboard | low wooden sideboard with two doors, a small vase and a desk clock on top | wood, ceramic | dark brown, cream |
| Bookcase | five-shelf wooden bookcase filled with plain books | wood, paper | dark brown, faded red and beige |
| SideTable | small four-legged wooden side table with an ashtray on top | wood, glass | dark brown, black |
| Armchair | upholstered single armchair | fabric, wood | faded brown, dark brown |
| PottedPlant | large-leaf indoor plant in a plain round pot | ceramic, leaves | dark green, brown |
| Wardrobe | two-door bedroom wardrobe with small knobs | painted wood | pale grey-blue, dark blue |
| Nightstand | small bedside nightstand with one drawer, nothing on top | painted wood | pale grey-blue, dark blue |
| StandingMirror | full-length standing mirror leaning on a back stand | painted wood, glass | pale grey-blue, dark mirror |
| ToyChest | wooden toy chest with a lid and a small ball beside it | painted wood | pale grey-blue, dark blue |

## 7. 수영장 소품 다시 정리 (2026-11-23 추가)

플레이 피드백(2026-11-23)으로 인트로·마지막 수영장에 남은 더미를 다시 본 결과다. 원래 목록(`object-list-and-prompt-guide.md` §3)에 있었지만 남은 목록에서 빠졌던 것과, 경로가 틀렸던 것을 바로잡는다.

| 파일 이름 | 설명 | 크기(m) | 우선 | 바꿀 더미 |
|---|---|---|---|---|
| `VAR_Pool_SideTable` | 작은 원형 흰 탁자(받침 기둥 하나). 위에 라디오를 올린다 | 0.6 × 0.5 | B | `Prop_SideTable_W/E`, `LagoonCourt_Table` |
| `VAR_Pool_DeckBench` | 데크의 흰 등받이 벤치(타일 또는 칠한 나무). `VAR_Pool_Bench`는 이미 선베드 이름이라 다른 이름을 쓴다 | 2.1 × 0.9 × 0.6 | B | `Prop_Bench_S1·S2·N1·N2` |
| `VAR_Pool_PlanterTree` | 낮은 흰 원형 화분에 심은 작은 야자수(줄기 하나, 잎 5~6장) | 화분 0.75 × 0.6, 전체 높이 2.2~3.8 | B | `Prop_Planter_*`, `LagoonCourt_Planter_*` |
| `VAR_Pool_BowlTree` | 넓고 낮은 흰 원형 화분(지름 2.2m)에 심은 큰 나무. 곧은 갈색 줄기 위에 둥근 잎 덩어리 | 높이 5.5~6.7 | B | `ChannelHall_Tree_N1~4·S1~4`(수로 홀, 같은 수영장에도 복사) |

- 큰 나무(`BowlTree`)도 오브젝트 하나라 VARCO로 뽑을 수 있다. 크기는 Unity에서 맞추므로 모델은 비율만 맞으면 된다. 원래 목록에서 이 나무를 `물속 야자수` 더미 경로로 적은 것은 잘못이다(물속 야자수가 아니라 화분 나무다).
- 수건 더미(`Prop_Towels`)는 2026-11-23 사용자 요청으로 지웠다. 만들지 않는다.
- 라디오는 이미 진짜 모델(`Assets/Prefabs/prop_radio.prefab`)이다. 누워 있던 회전만 2026-11-23에 고쳤다.

| 파일 이름 | [오브젝트 영문 설명] | [재질] | [색] |
|---|---|---|---|
| SideTable | small round poolside side table on a single pedestal with a round base | painted metal, plastic top | off-white, pale grey |
| DeckBench | long poolside bench with a low backrest | painted wood or white tile | off-white, pale peach |
| PlanterTree | small palm tree in a low round white planter pot | ceramic pot, leaves | off-white pot, soft green leaves |
| BowlTree | tall tree with a straight trunk and a round leafy crown, planted in a wide low round white planter bowl | ceramic bowl, bark, leaves | off-white bowl, brown trunk, soft green leaves |
