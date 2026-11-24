# DEPTH — 남은 오브젝트 (2026-11-24 갱신)

> **✅ 모두 완료(2026-11-24).** 수영모 인외(얼굴 앞 조각·물안경을 Unity에서 고침)와 사이드 테이블까지 넣었다. 다음은 후처리.
> 2026-11-24: 마네킹은 기존 모델을 그대로 쓴다(VARCO는 몸선이 드러남). 거실 포스터 3장은 넣었다.
> 2026-11-24에 데크 벤치·화분 야자수·큰 그릇 나무·거실 책장을 넣었다(책장은 책이 깨져서 나무 틀만 쓰고 책은 코드로 새로 세움).

VARCO로 아직 뽑지 않은 것만 모았다. 이미 바꾼 것, 더미로 두기로 한 것(쌓은 의자, 수건 더미, 타일 집 등)은 뺐다.
전체 목록과 규칙은 `object-list-and-prompt-guide.md`(원래 문서)와 `object-list-addendum-2026-11-20.md`(추가 목록)에 있다.

**넣는 법:** FBX를 `Assets/VARCO/` 바로 아래에 **아래 파일 이름 그대로** 올리면, 방 폴더로 옮기고 프리팹을 만들어 같은 자리 더미와 바꾼다.
- 앞면은 **+Z**, 원점은 **바닥 가운데**. 크기는 Unity에서 맞추므로 비율만 맞으면 된다.

| # | 방 | 파일 이름 | 설명 | 크기(m) | 우선 | 바꿀 더미 |
|---|---|---|---|---|---|---|
| 1 | 수영장 홀 끝(Basin 너머) | ✅ ~~`VAR_PoolHall_SwimCapFigure`~~ (완료 2026-11-24) | 수영모·물안경을 쓴 얼굴 없는 키 큰 형체. 비치타월을 망토처럼 둘러 몸선이 안 보임. 가만히 서서 플레이어를 정면으로 본다 | 높이 2.2~2.4 | A | `Mannequin_Distant`(이것만 바꿈) |
| — | 인트로 · 금 간 복도 | ~~`VAR_Pool_Mannequin`~~ | **뽑지 않음(2026-11-24)**: 기존 마네킹 모델을 그대로 쓴다 | — | — | `Mannequin_InTubeSpot`, `Mannequin_Standing` 유지 |
| 2 | 인트로 · 마지막 수영장 | ✅ ~~`VAR_Pool_SideTable`~~ (완료 2026-11-24) | 작은 원형 흰 탁자(받침 기둥 하나). 위에 라디오를 올린다 | 0.6 × 0.5 | B | `Prop_SideTable_W/E`, `LagoonCourt_Table` |
| 3 | 인트로 · 마지막 수영장 | ✅ ~~`VAR_Pool_DeckBench`~~ (완료 2026-11-24) | 데크의 흰 등받이 벤치 | 2.1 × 0.9 × 0.6 | B | `Prop_Bench_S1·S2·N1·N2` |
| 4 | 인트로 · 마지막 수영장 | ✅ ~~`VAR_Pool_PlanterTree`~~ (완료 2026-11-24) | 낮은 흰 원형 화분에 심은 작은 야자수(줄기 하나, 잎 5~6장) | 화분 0.75 × 0.6, 전체 2.2~3.8 | B | `Prop_Planter_*`, `LagoonCourt_Planter_*` |
| 5 | 인트로 · 마지막 수영장 | ✅ ~~`VAR_Pool_BowlTree`~~ (완료 2026-11-24) | 넓고 낮은 흰 원형 화분(지름 2.2)에 심은 큰 나무. 곧은 갈색 줄기, 둥근 잎 덩어리 | 높이 5.5~6.7 | B | `ChannelHall_Tree_N1~4·S1~4` |
| 6 | 밤의 거실 | ✅ ~~`VAR_LivingRoom_Bookcase`~~ (완료 2026-11-24) | 책이 꽂힌 나무 책장(5단) | 0.9 × 1.9 × 0.34 | B | 거실 동쪽 벽 `Bookcase` |

**추천 순서:** 1 수영모 인외 → 2 사이드 테이블(라디오 자리).
- 마네킹이 게임 앞쪽 세 곳에 몰려 있어서, 수영장 홀 끝에 멀리 선 하나만 다른 인외로 바꾼다. 튜브 자리(찾기)와 금 간 복도(미끼)는 "같은 모델이라 헷갈리게 하는" 짝이라 그대로 둔다.
- 받는 코드는 준비돼 있다(`SwapDistantMannequin`): FBX를 `Assets/VARCO/VAR_PoolHall_SwimCapFigure.fbx`로 올리면 마네킹을 끄고 같은 자리·방향에 세운다.

### ✅ 2D 그림 (2026-11-24 추가, 완료): 거실 북쪽 벽 포스터 3장
빈 가족 액자 3개 자리를 노란 방처럼 **틀 없이 그림만** 붙인다. PNG를 `Assets/VARCO/LivingRoom/2D/`에 아래 이름으로 넣으면 바로 바뀐다(크기는 액자의 1.2배, 가로는 그림 비율).
세로 **4:5**. 프롬프트 끝에 공통으로 `flat front view, orthographic, no frame, no border, no text, faded 1990s print, slightly washed-out colors`를 붙인다.

| 자리 | 파일 이름 | 내용 | 프롬프트 |
|---|---|---|---|
| 왼쪽 | `VAR_LivingRoom_Poster_A.png` | 바랜 가족사진, 얼굴은 빛 번짐으로 날아감 | old faded family photograph, a family of three standing in front of a sofa in a dim living room, faces completely blown out by light glare, no facial features |
| 가운데 | `VAR_LivingRoom_Poster_B.png` | 아무도 없는 90년대 야외 수영장, 분홍 플라밍고 튜브 하나 | faded 1990s photograph of an empty outdoor swimming pool at dusk, one pink flamingo float on the water, nobody around |
| 오른쪽 | `VAR_LivingRoom_Poster_C.png` | 아이 크레용 그림: 집, 해, 지붕보다 높은 파란 물결 | child's crayon drawing on paper: a small house, a sun, and big blue wavy water drawn higher than the roof |

---

## 프롬프트 (그대로 붙여 넣기)

### 1. 수영모 인외 `VAR_PoolHall_SwimCapFigure`
```
A single tall thin faceless humanoid figure standing upright and still, wearing a tight white swim cap and dark round swimming goggles,
a large faded striped beach towel draped over the shoulders like a long cloak down to the ankles, hiding the body shape,
arms hidden under the towel, bare pale feet, faceless smooth head below the goggles, no facial features, neutral pose,
dreamcore / liminal space aesthetic, clean simplified realistic style, slightly elongated proportions, soft rounded bevels,
matte materials (rubber cap, cotton towel), muted faded colors (off-white, pale blue and peach stripes, grey),
no text, no logos, no brand marks,
isolated on a plain light grey background, full object visible, centered,
3/4 front view from slightly above, soft even studio lighting, no harsh shadows,
no other objects, product render, high detail silhouette
```

### (뽑지 않음) 마네킹 `VAR_Pool_Mannequin`
```
A single faceless white store mannequin standing upright in a neutral pose, arms relaxed at the sides,
faceless smooth head, no facial features, dreamcore / liminal space aesthetic,
clean simplified realistic style, slightly toy-like proportions, soft rounded bevels,
matte materials (smooth fiberglass), muted faded colors (off-white, pale grey),
1990s everyday object, no text, no logos, no brand marks, no clothes, no stand base,
isolated on a plain light grey background, full object visible, centered,
3/4 front view from slightly above, soft even studio lighting, no harsh shadows,
no other objects, no people, product render, high detail silhouette
```

### 2. 사이드 테이블 `VAR_Pool_SideTable`
```
A single small round poolside side table on a single pedestal with a round base, dreamcore / liminal space aesthetic,
clean simplified realistic style, slightly toy-like proportions, soft rounded bevels,
matte materials (painted metal, plastic top), muted faded colors (off-white, pale grey),
1990s everyday object, no text, no logos, no brand marks,
isolated on a plain light grey background, full object visible, centered,
3/4 front view from slightly above, soft even studio lighting, no harsh shadows,
no other objects, no people, product render, high detail silhouette
```

### 3. 데크 벤치 `VAR_Pool_DeckBench`
```
A single long poolside bench with a low backrest, dreamcore / liminal space aesthetic,
clean simplified realistic style, slightly toy-like proportions, soft rounded bevels,
matte materials (painted wood or white tile), muted faded colors (off-white, pale peach),
1990s everyday object, no text, no logos, no brand marks,
isolated on a plain light grey background, full object visible, centered,
3/4 front view from slightly above, soft even studio lighting, no harsh shadows,
no other objects, no people, product render, high detail silhouette
```

### 4. 화분 야자수 `VAR_Pool_PlanterTree`
```
A single small palm tree in a low round white planter pot, dreamcore / liminal space aesthetic,
clean simplified realistic style, slightly toy-like proportions, soft rounded bevels,
matte materials (ceramic pot, leaves), muted faded colors (off-white pot, soft green leaves),
1990s everyday object, no text, no logos, no brand marks,
isolated on a plain light grey background, full object visible, centered,
3/4 front view from slightly above, soft even studio lighting, no harsh shadows,
no other objects, no people, product render, high detail silhouette
```

### 5. 큰 그릇 나무 `VAR_Pool_BowlTree`
```
A single tall tree with a straight trunk and a round leafy crown, planted in a wide low round white planter bowl,
dreamcore / liminal space aesthetic, clean simplified realistic style, slightly toy-like proportions, soft rounded bevels,
matte materials (ceramic bowl, bark, leaves), muted faded colors (off-white bowl, brown trunk, soft green leaves),
1990s everyday object, no text, no logos, no brand marks,
isolated on a plain light grey background, full object visible, centered,
3/4 front view from slightly above, soft even studio lighting, no harsh shadows,
no other objects, no people, product render, high detail silhouette
```

### 6. 거실 책장 `VAR_LivingRoom_Bookcase`
```
A single five-shelf wooden bookcase filled with plain books, dreamcore / liminal space aesthetic,
clean simplified realistic style, slightly toy-like proportions, soft rounded bevels,
matte materials (wood, paper), muted faded colors (dark brown, faded red and beige),
1990s everyday object, no text, no logos, no brand marks,
isolated on a plain light grey background, full object visible, centered,
3/4 front view from slightly above, soft even studio lighting, no harsh shadows,
no other objects, no people, product render, high detail silhouette
```
