# DEPTH — 남은 오브젝트 (2026-11-23 기준)

VARCO로 아직 뽑지 않은 것만 모았다. 이미 바꾼 것, 더미로 두기로 한 것(쌓은 의자, 수건 더미, 타일 집 등)은 뺐다.
전체 목록과 규칙은 `object-list-and-prompt-guide.md`(원래 문서)와 `object-list-addendum-2026-11-20.md`(추가 목록)에 있다.

**넣는 법:** FBX를 `Assets/VARCO/` 바로 아래에 **아래 파일 이름 그대로** 올리면, 방 폴더로 옮기고 프리팹을 만들어 같은 자리 더미와 바꾼다.
- 앞면은 **+Z**, 원점은 **바닥 가운데**. 크기는 Unity에서 맞추므로 비율만 맞으면 된다.

| # | 방 | 파일 이름 | 설명 | 크기(m) | 우선 | 바꿀 더미 |
|---|---|---|---|---|---|---|
| 1 | 인트로 · 금 간 복도 | `VAR_Pool_Mannequin` | 얼굴 없는 흰 마네킹, 서 있는 자세. **머리를 따로 나눈 메시**(이름 `Head`, 목에 피벗) | 높이 1.8 | 찾기 | `Mannequin_InTubeSpot`, `Mannequin_Standing` |
| 2 | 인트로 · 마지막 수영장 | `VAR_Pool_SideTable` | 작은 원형 흰 탁자(받침 기둥 하나). 위에 라디오를 올린다 | 0.6 × 0.5 | B | `Prop_SideTable_W/E`, `LagoonCourt_Table` |
| 3 | 인트로 · 마지막 수영장 | `VAR_Pool_DeckBench` | 데크의 흰 등받이 벤치 | 2.1 × 0.9 × 0.6 | B | `Prop_Bench_S1·S2·N1·N2` |
| 4 | 인트로 · 마지막 수영장 | `VAR_Pool_PlanterTree` | 낮은 흰 원형 화분에 심은 작은 야자수(줄기 하나, 잎 5~6장) | 화분 0.75 × 0.6, 전체 2.2~3.8 | B | `Prop_Planter_*`, `LagoonCourt_Planter_*` |
| 5 | 인트로 · 마지막 수영장 | `VAR_Pool_BowlTree` | 넓고 낮은 흰 원형 화분(지름 2.2)에 심은 큰 나무. 곧은 갈색 줄기, 둥근 잎 덩어리 | 높이 5.5~6.7 | B | `ChannelHall_Tree_N1~4·S1~4` |
| 6 | 밤의 거실 | `VAR_LivingRoom_Bookcase` | 책이 꽂힌 나무 책장(5단) | 0.9 × 1.9 × 0.34 | B | 거실 동쪽 벽 `Bookcase` |

**추천 순서:** 1 마네킹(찾기 규칙의 이상한 점) → 2 사이드 테이블(라디오 자리) → 나머지.
- 마네킹은 지금 쓰는 모델에 돌아서서 노려보는 코드가 붙어 있어서, 모델이 오면 머리 피벗을 맞추는 일이 추가로 든다.

---

## 프롬프트 (그대로 붙여 넣기)

### 1. 마네킹 `VAR_Pool_Mannequin`
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
