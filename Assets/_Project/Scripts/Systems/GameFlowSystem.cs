using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 게임 오버·레벨업 선택 중이면 시간을 멈추고(Time.timeScale = 0), 재시작 요청이 오면 판을 처음 상태로 되돌린다.
    /// 생존 시간도 여기서 잰다. 레벨·업그레이드 초기화는 ProgressionSystem이 맡는다.
    /// 모든 이동·스폰·쿨다운이 deltaTime을 쓰므로 timeScale 하나로 각 시스템을 건드리지 않고 전부 멈출 수 있다.
    /// UnityEngine.Time은 관리 API라 Burst를 쓰지 않는다. 재시작은 드물게 일어나서 메인 스레드 구조 변경으로 충분하다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PlayerInputSystem))]
    public partial struct GameFlowSystem : ISystem
    {
        EntityQuery _bulletQuery;
        EntityQuery _enemyQuery;
        EntityQuery _explosionQuery;
        EntityQuery _gemQuery;
        EntityQuery _shockwaveQuery;

        public void OnCreate(ref SystemState state)
        {
            _bulletQuery = SystemAPI.QueryBuilder().WithAll<Bullet>().Build();
            _enemyQuery = SystemAPI.QueryBuilder().WithAll<Enemy>().Build();
            _explosionQuery = SystemAPI.QueryBuilder().WithAll<Explosion>().Build();
            _gemQuery = SystemAPI.QueryBuilder().WithAll<XpGem>().Build();
            _shockwaveQuery = SystemAPI.QueryBuilder().WithAll<Shockwave>().Build();
            state.RequireForUpdate<GameState>();
            state.RequireForUpdate<GameStats>();

            // 게임 오버 상태로 Play를 멈췄다 다시 켜도 시간이 멈춰 있지 않게
            UnityEngine.Time.timeScale = 1f;
        }

        public void OnDestroy(ref SystemState state)
        {
            UnityEngine.Time.timeScale = 1f;
        }

        public void OnUpdate(ref SystemState state)
        {
            GameState game = SystemAPI.GetSingleton<GameState>();
            if (game.RestartRequested)
            {
                Restart(ref state);
                return;
            }

            // 게임 오버, 레벨업 선택 중, 일시정지 중에는 멈춘다
            bool choosingUpgrade = SystemAPI.TryGetSingleton(out LevelUpOffer offer) && offer.Active;
            UnityEngine.Time.timeScale = game.IsGameOver || choosingUpgrade || game.Paused ? 0f : 1f;
            RefRW<GameStats> stats = SystemAPI.GetSingletonRW<GameStats>();
            stats.ValueRW.SurvivalTime += SystemAPI.Time.DeltaTime;

            // 제한 시간까지 버티면 클리어로 판을 끝낸다 (게임 오버와 같은 경로라 멈춤·보상·결과 화면을 그대로 쓴다)
            if (!game.IsGameOver && SystemAPI.TryGetSingleton(out RunRules rules) && rules.DurationSeconds > 0f
                && stats.ValueRO.SurvivalTime >= rules.DurationSeconds)
            {
                stats.ValueRW.SurvivalTime = rules.DurationSeconds;
                game.IsGameOver = true;
                game.Cleared = true;
                SystemAPI.SetSingleton(game);
            }
        }

        void Restart(ref SystemState state)
        {
            state.EntityManager.DestroyEntity(_bulletQuery);
            state.EntityManager.DestroyEntity(_enemyQuery);
            state.EntityManager.DestroyEntity(_explosionQuery);
            state.EntityManager.DestroyEntity(_gemQuery);
            state.EntityManager.DestroyEntity(_shockwaveQuery);

            foreach (var (health, transform, position, facing) in SystemAPI
                         .Query<RefRW<PlayerHealth>, RefRW<LocalTransform>, RefRW<PlayerPosition>, RefRW<FacingDirection>>())
            {
                health.ValueRW.Value = health.ValueRO.Max;
                health.ValueRW.InvulnerableTimer = 0f;
                transform.ValueRW.Position = float3.zero;
                position.ValueRW.Value = float2.zero;
                facing.ValueRW.Value = new float2(1f, 0f);
            }

            foreach (RefRW<EnemySpawner> spawner in SystemAPI.Query<RefRW<EnemySpawner>>())
            {
                spawner.ValueRW.Cooldown = spawner.ValueRO.Interval;
                spawner.ValueRW.EliteCooldown = spawner.ValueRO.EliteInterval;
            }
            // 보스 등장 횟수를 되돌려야 새 판에서도 3분에 첫 보스가 나온다 (보스 엔티티는 위의 적 삭제로 함께 지워짐)
            foreach (RefRW<BossSpawner> bossSpawner in SystemAPI.Query<RefRW<BossSpawner>>())
                bossSpawner.ValueRW.Spawned = 0;

            // 부서진 문을 되돌리고 방을 닫는다 (WallSystem이 다음 프레임 처음에 처리)
            if (SystemAPI.HasSingleton<WallDamage>())
                SystemAPI.SetSingleton(new WallDamage { Dirty = true });

            // 플레이어가 원점으로 순간 이동하므로 카메라도 부드럽게 따라가지 않고 바로 맞춘다
            foreach (RefRW<CameraView> view in SystemAPI.Query<RefRW<CameraView>>())
                view.ValueRW.Snapped = false;

            SystemAPI.SetSingleton(new GameState());
            SystemAPI.SetSingleton(new GameStats());
            UnityEngine.Time.timeScale = 1f;
        }
    }
}
