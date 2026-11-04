# 증강 (Augments)

5레벨마다(5, 10, 15 …) 일반 강화 대신 황금색 증강 카드 3장 중 1장을 고른다. 같은 증강은 한 판에 한 번만 나오고, 다 고르면 다시 일반 강화가 나온다.

증강은 능력치를 조금 올리는 게 아니라 **규칙을 바꾸고, 큰 이득과 큰 대가를 함께 준다**. 그래야 고르는 게 고민되고 판마다 빌드 성격이 달라진다.

| 빌드 | 증강 | 이득 | 대가 | 바뀌는 곳 |
|---|---|---|---|---|
| 연사 | 쌍둥이 총구 | 한 번에 2발 (나란히 띄워 20° 부채꼴, 나가자마자 합쳐지지 않게) | 피해 -30% | `AutoFire.ShotsPerVolley` |
| 연사 | 과열 | 발사 간격 -50% | 피해 -40% | `AutoFire.Interval` |
| 한방 | 대구경 | 레벨 2 총알 발사 | 발사 간격 ×2 | `AutoFire.StartLevel` |
| 한방 | 탄성 | 튕길 때마다 그 총알 피해 +20% (누적) | 총알 속도 -20% | `Bullet.Bounces`, `BulletLevelSettings.BounceDamageBonus` |
| 범위 | 과부하 | 최대 레벨 효과 ×2.5 | 발사 간격 ×3 | `MaxLevelEffect` |
| 범위 | 연쇄 합성 | 합성할 때마다 작은 폭발 | 피해 -30% | `MaxLevelEffect.MergeBlast*`, `BulletMergeSystem` |
| 합성 규칙 | 레벨 5 | 최대 레벨 5, 최대 레벨 효과 ×2.5 | 한 번 더 합쳐야 터짐 | `BulletLevelSettings.MaxLevel` |
| 합성 규칙 | 끈기 | 총알 지속 시간 ×2 | 피해 -20% | `BulletIdleSettings.Lifetime` |

## 구조

- 수치: `AugmentTuning` (`Systems/ProgressionSystem.cs`). HUD 설명도 같은 값으로 만든다
- 적용: `ProgressionSystem.ApplyAugment` — 고른 순간 해당 싱글톤 값에 한 번 곱하거나 켠다
- 기록: `Augments.Taken` 비트. 재시작하면 0, 바뀐 값은 `BaseStats`로 되돌린다
- 화면: 레벨업 화면을 그대로 쓰고 `.augment` 클래스로 황금색. 카드 설명은 첫 줄이 얻는 것(`augment.{종류}.gain`), 둘째 줄이 잃는 것(빨간색, `augment.cost.*`)

## 나중에

- 수치는 밸런스 작업 때 조정
- 증강끼리 겹칠 때(예: 쌍둥이 총구 + 대구경)의 강함 확인
- 빌드를 늘릴 때: `AugmentType`에 추가 → `ApplyAugment` → `DescribeAugment` → CSV 문구
