using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 게임 씬을 떠나면(로비로 이동) 실행 중에 생긴 엔티티(총알·적·젬·폭발)를 지운다.
    /// ECS 월드는 씬과 따로 살아서, SubScene에서 온 엔티티는 씬과 함께 내려가지만 실행 중에 만든 엔티티는 남는다.
    /// 게임 씬의 표시인 GameState 싱글톤이 사라졌는데 이런 엔티티가 남아 있으면 정리한다.
    /// 씬을 어떤 방법으로 떠나든 처리되고, 로비에서는 빈 쿼리 확인만 해서 비용이 거의 없다.
    /// </summary>
    [BurstCompile]
    public partial struct GameSceneExitCleanupSystem : ISystem
    {
        EntityQuery _leftoverQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _leftoverQuery = new EntityQueryBuilder(Unity.Collections.Allocator.Temp)
                .WithAny<Bullet, Enemy, XpGem, Explosion, Shockwave>()
                .Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<GameState>() || _leftoverQuery.IsEmpty) return;
            state.EntityManager.DestroyEntity(_leftoverQuery);
        }
    }
}
