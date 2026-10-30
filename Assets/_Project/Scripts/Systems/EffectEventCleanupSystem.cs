using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 프레임 맨 앞에서 지난 프레임의 연출 이벤트를 비운다.
    /// EffectPresenter는 읽기만 하므로, 연출이 없는 씬에서도 이벤트가 끝없이 쌓이지 않는다.
    /// BeginSimulation ECB보다 먼저 돌아야, 그 ECB가 재생하며 넣는 이벤트(폭발 등)가 지워지지 않고 이번 프레임에 보인다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(BeginSimulationEntityCommandBufferSystem))]
    [BurstCompile]
    public partial struct EffectEventCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EffectQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.GetSingletonBuffer<EffectEvent>().Clear();
        }
    }
}
