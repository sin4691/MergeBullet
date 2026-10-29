# Performance

성능 관련 변경은 이 문서에 전후 수치를 남긴다. 같은 조건으로 비교해야 의미가 있으므로 아래 방법을 그대로 따른다.

## 측정 방법 (벤치마크 모드)

1. **Scene 뷰가 화면에 보이지 않게 한다** (Game 뷰 탭만 보이게). Scene 뷰가 보이면 에디터가 매 프레임 그것까지 그려서 프레임 시간이 약 2배가 된다.
2. Hierarchy에서 `SampleScene > ArenaSubScene > Arena`(정육면체 아이콘)를 선택한다.
3. Inspector `Arena Authoring > Benchmark`에서 **Benchmark Mode**를 켠다. 기본 조건은 그대로 둔다.
4. Play. 워밍업이 지나고 적이 최대 수까지 차면 측정을 시작한다 (오래 걸리면 워밍업의 3배 시점에 시작).
5. 측정이 끝나면 Console에 요약이 찍힌다.
   ```
   [Benchmark] 2034 frames | avg 4.92 ms (203 FPS) | p95 5.58 ms | max 29.25 ms | bullets ~847 (target 1000) | enemies ~300 (max 300)
   [Benchmark] top 20 markers (ms per frame, jobs = sum of worker threads):
       2.089  GUIView.RepaintAll.PlayerLoopController
       1.198  PlayerLoop
       0.411  BulletEnemyBounceJob (Burst)
       ...
   ```
6. **측정 후 Benchmark Mode를 끈다.** 켠 채로 커밋하지 않는다.

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
| 워밍업 | 8초 이상, 적이 최대 수에 도달할 때까지 |
| 측정 | 10초 |

### 측정 환경 기록

결과가 환경에 따라 크게 달라지므로 함께 적는다.

- 에디터 Play 모드인지, 빌드인지
- Scene 뷰가 보였는지
- `Jobs > Burst > Safety Checks`, `Jobs > JobsDebugger` 켜짐 여부 (에디터 기본값은 켜짐, 켜져 있으면 느리다)
- CPU / GPU

## 시스템별 비용 읽는 법

요약 두 번째 부분은 이 프로젝트의 시스템·잡 이름과 엔진 주요 단계(PlayerLoop, 렌더링 등)가 들어간 Profiler 마커의 프레임당 평균 시간이다.

- `Default World ...System`: 메인 스레드에서 그 시스템이 잡을 예약하는 등 직접 쓴 시간
- `...Job (Burst)`: 워커 스레드에서 실제 계산한 시간. **여러 스레드의 시간을 합친 값(CPU 사용량)**이라 프레임 시간과 바로 더해지지 않는다
- `PlayerLoop`: 메인 스레드의 게임 한 프레임 전체
- `GUIView.RepaintAll...`, `EditorLoop`: 에디터 비용. 빌드에는 없다

더 자세히 보려면 `Window > Analysis > Profiler`의 Timeline에서 같은 이름을 검색한다.

## 기록

| 날짜 | 변경 | 환경 | 총알 / 적 | avg ms (FPS) | p95 ms | max ms |
|---|---|---|---|---|---|---|
| 2026-10-29 | 기준값 (main 8a4ba40, #14까지) | 에디터 Play, Scene 뷰 **보임** | ~845 (target 1000) / 300 | 9.54 (105) | 10.62 | 20.24 |
| 2026-10-29 | 기준값 재측정 (#15 + 시스템별 집계) | 에디터 Play, Scene 뷰 **가림** | ~847 (target 1000) / 300 | 4.92 (203) | 5.58 | 29.25 |

환경: i5-12400F, RTX 3080, RAM 16GB, Unity 6000.6.7f1, Burst Safety Checks 에디터 기본값

첫 번째 기록은 Scene 뷰가 보이는 상태라 이후 측정과 비교하지 않는다. **기준값은 두 번째 기록이다.**

### 2026-10-29 시스템별 분석 (Scene 뷰 가림)

| 마커 | ms / frame | 비고 |
|---|---|---|
| GUIView.RepaintAll.PlayerLoopController | 2.09 | 에디터의 Game 뷰 다시 그리기. 빌드에는 없음 |
| PlayerLoop | 1.20 | 메인 스레드의 게임 프레임 전체 |
| BulletEnemyBounceJob | 0.41 | 워커 합계 |
| FindMergePartnerJob | 0.36 | 워커 합계 |
| EndSimulationEntityCommandBufferSystem | 0.31 | 합성·소멸 삭제 명령 재생 (프레임당 ~150개) |
| EntitiesGraphicsSystem | 0.19 | 렌더링 준비 |
| 그 외 이 프로젝트 잡·시스템 | 각 0.13 이하 | |

**결론**: 이 부하(총알 ~850, 적 300)에서 게임 로직은 병목이 아니다. 프레임 시간 대부분은 에디터 비용이다.
가장 비싼 잡을 절반으로 줄여도 프레임 시간은 몇 % 수준이라, 지금은 잡 최적화를 하지 않는다.
최적화는 부하를 키울 때(더 큰 아레나, 더 많은 적) 또는 빌드·모바일 측정에서 병목이 보일 때 이 표를 기준으로 진행한다.
