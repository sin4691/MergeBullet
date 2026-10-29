# Performance

성능 관련 변경은 이 문서에 전후 수치를 남긴다. 같은 조건으로 비교해야 의미가 있으므로 아래 방법을 그대로 따른다.

## 측정 방법 (벤치마크 모드)

1. Hierarchy에서 `SampleScene > ArenaSubScene > Arena`(정육면체 아이콘)를 선택한다.
2. Inspector `Arena Authoring > Benchmark`에서 **Benchmark Mode**를 켠다. 기본 조건은 그대로 둔다.
3. Play. 워밍업 동안 적이 최대 수까지 차고, 이후 측정 시간 동안 프레임 시간을 모은다.
4. 측정이 끝나면 Console에 한 줄이 찍힌다.
   ```
   [Benchmark] 1450 frames | avg 6.90 ms (145 FPS) | p95 8.20 ms | max 14.10 ms | bullets ~998 (target 1000) | enemies ~300 (max 300)
   ```
5. **측정 후 Benchmark Mode를 끈다.** 켠 채로 커밋하지 않는다.

벤치마크 모드에서는:
- 총알을 목표 수로 계속 보충한다 (합성·소멸로 줄어든 만큼 레벨 1을 임의 위치에 생성)
- 적은 0.02초마다 스폰되어 최대 수를 유지하고, 죽지 않는다 (피해·충돌 계산은 그대로)
- 플레이어는 피해를 받지 않고, 레벨업 선택으로 멈추지 않는다

`bullets`는 목표보다 조금 낮게 나온다. 매 프레임 합성·폭발로 사라진 총알이 다음 프레임에 보충되므로, 측정 시점에는 그만큼 빠져 있다.
(예: 목표 1000, 측정 ~845 → 프레임마다 약 150개가 합성으로 사라지고 다시 채워지는 정상 상태)
`target`·`max` 값이 기본 조건과 다르면 조건이 다른 측정이므로 비교하지 않는다.

### 기본 조건

| 항목 | 값 |
|---|---|
| 총알 목표 수 | 1000 (레벨 1 기준 아레나 면적의 약 31%) |
| 적 최대 수 | 300 |
| 워밍업 | 8초 |
| 측정 | 10초 |

### 측정 환경 기록

결과가 환경에 따라 크게 달라지므로 함께 적는다.

- 에디터 Play 모드인지, 빌드인지
- `Jobs > Burst > Safety Checks`, `Jobs > JobsDebugger` 켜짐 여부 (에디터 기본값은 켜짐, 켜져 있으면 느리다)
- Game 뷰 해상도, Stats 창 열림 여부
- CPU / GPU

## 시스템별 비용 보기 (선택)

Console 요약은 프레임 전체 시간이다. 어느 시스템이 비싼지는 Profiler로 본다.

1. `Window > Analysis > Profiler` → Play → 측정 구간의 프레임 하나 클릭
2. 아래 패널을 **Timeline**으로 바꾸고, 검색에 잡 이름(`FindMergePartnerJob`, `BulletEnemyBounceJob`, `SeparateEnemiesJob` 등) 입력
3. Worker 줄의 막대에 마우스를 올려 ms 확인

## 기록

| 날짜 | 변경 | 환경 | 총알 / 적 | avg ms (FPS) | p95 ms | max ms |
|---|---|---|---|---|---|---|
| 2026-10-29 | 기준값 (main 8a4ba40, #14까지) | 에디터 Play, Safety Checks 기본값 | ~845 (target 1000) / 300 | 9.54 (105) | 10.62 | 20.24 |

환경: i5-12400F, RTX 3080, RAM 16GB, Unity 6000.6.7f1
