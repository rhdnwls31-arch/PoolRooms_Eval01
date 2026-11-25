# 목각인형 화실 — VARCO 3D 프롬프트 (2026-11-25)

기획: `design/quick-designs/weirdcore-door-descent-v2.md` §6-3b2.

5종 모두 **같은 인형**이어야 바꿔 끼울 때 티가 안 난다. 그래서 공통 문장을 앞에 똑같이 두고, 자세 문장만 바꾼다. 가능하면 첫 번째(A)를 뽑은 뒤 그 이미지를 참고 이미지로 넣어 나머지를 뽑는다.

넣을 곳: `Assets/VARCO/` (가져올 때 `Assets/VARCO/DollRoom/`로 옮김). 키 1.75m, 원점 발밑, 정면 +Z로 맞춘다.

## 공통 문장 (모든 프롬프트 앞에)

> A life-size wooden artist's drawing mannequin, 1.75 m tall, human proportions, smooth faceless egg-shaped head with no features, visible ball joints at neck, shoulders, elbows, wrists, hips, knees and ankles, segmented limbs, pale worn beech wood with faint grain and old graphite smudges, slightly dusty, no stand, no base, standing on its own feet, plain studio lighting, clean single mesh,

## 자세 5종

| 파일 이름 | 쓰는 단계 | 자세 문장 (공통 문장 뒤에) |
|---|---|---|
| `VAR_DollRoom_Doll_Bowed.fbx` | 처음 | head bowed down toward the chest, both arms hanging straight down at the sides, feet together, front facing |
| `VAR_DollRoom_Doll_HeadUp.fbx` | 1번 돌아봄 · 찾기 인형 | head raised and facing straight forward, both arms hanging straight down at the sides, feet together, front facing |
| `VAR_DollRoom_Doll_ArmHalf.fbx` | 2번 돌아봄 | head facing straight forward, right arm raised forward to about 45 degrees with the hand open, left arm hanging down, front facing |
| `VAR_DollRoom_Doll_ArmReach.fbx` | 3번 돌아봄 | head facing straight forward, right arm fully extended straight forward at shoulder height, index finger pointing at the viewer, left arm hanging down, one foot slightly forward as if mid-step, front facing |
| `VAR_DollRoom_Doll_PointSide.fbx` | 마지막(길 열기) | body facing front, head turned 90 degrees to its own left, left arm fully extended straight out to its own left at shoulder height, index finger pointing, right arm hanging down |

- PointSide: 서쪽 벽 인형은 동쪽(길 가운데)을 보고 서서 자기 왼쪽 = 북쪽(문)을 가리킨다. 동쪽 벽 인형은 Unity에서 좌우를 뒤집어 쓴다.
- 손가락이 뭉개지면 "open hand"로 바꿔도 된다. 가리키는 방향은 팔 방향으로 충분하다.

---

## 소품 (2026-11-25)

### 재활용 (이미 넣음)

| 자리 | 쓴 모델 | 비고 |
|---|---|---|
| 남쪽 입구 문(등 뒤에 닫힌 문), 북쪽 출구 문 | `VAR_FloralCorridor_Door` | 낡은 갈색 나무 현관문. 문 구멍 1.2×2.1m에 맞춰 늘림 |
| 남서·북동 구석 | `VAR_Yellow_CardboardBoxes` | 상자 더미 2개 |

파란 침실 의자·문은 텍스처 자체가 푸른빛이라 쓰지 않았다.

### 새로 뽑을 것 (VARCO 3D)

넣을 곳: `Assets/VARCO/` (가져올 때 `DollRoom` 폴더로). 원점 발밑, 정면 +Z. 코드가 이미 기다리고 있어서, 들어오면 더미 대신 높이를 맞춰 자동으로 씌운다.

| 파일 이름 | 쓰는 자리 | 프롬프트 |
|---|---|---|
| `VAR_DollRoom_SheetTall.fbx` | 북서 구석 키 큰 가구(1.9m) | A tall wardrobe completely covered by an old white cotton dust sheet, the cloth draped loosely with soft folds and creases, hanging to the floor with a slightly uneven hem, faint grey dust stains, the furniture shape only readable through the cloth, no wood visible, realistic, clean single mesh |
| `VAR_DollRoom_SheetLow.fbx` | 남서 낮은 장, 남동 탁자, 북동 궤짝 (3곳) | A low wide chest of drawers completely covered by an old white cotton dust sheet, the cloth draped loosely over the top and sides with soft folds, hem touching the floor, faint grey dust stains, no wood visible, realistic, clean single mesh |
| `VAR_DollRoom_SheetChair.fbx` | 남동 구석 의자 | An armchair completely covered by an old white cotton dust sheet, cloth draped over the backrest and armrests with deep soft folds, hem pooling slightly on the floor, faint grey dust stains, the chair shape only readable through the cloth, realistic, clean single mesh |
| `VAR_DollRoom_Easel.fbx` | 남쪽 두 곳 | A wooden artist's studio easel on three legs, worn pale wood with paint drips, holding a blank off-white canvas on the ledge, the canvas slightly tilted back, front facing, realistic, clean single mesh |

### 새로 뽑을 것 (VARCO 2D)

| 파일 이름 | 비율 | 프롬프트 |
|---|---|---|
| `Assets/VARCO/DollRoom/2D/VAR_DollRoom_Sketch.png` | 세로 3:4 (예: 768×1024) | a single pencil croquis sketch on off-white drawing paper, a faceless person standing straight with arms at the sides drawn in quick loose graphite lines, soft smudges and erased guide lines, the figure centered, plain paper fills the whole frame edge to edge, no border, no text, flat even lighting |

벽의 크로키 종이 30여 장은 모두 이 그림 한 장을 쓴다(모두 같은 사람이라 더 섬뜩하다).
