using MergeBullet.Components;
using MergeBullet.Meta;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 판 밖(저장 데이터)의 선택 캐릭터를 판 안(ECS)에 적용하는 다리. 게임 씬이 로드되면 한 번,
    /// 레벨 4 특수효과 종류(MaxLevelEffect.Kind)와 플레이어 색을 그 캐릭터로 바꾼다.
    /// 벤치마크는 저장 상태와 상관없이 같은 조건이어야 해서 항상 기본 캐릭터(Blaster)로 한다.
    /// 저장 데이터는 관리 코드라 Burst를 쓰지 않는다 (게임 씬 진입 때 한 번만 실행).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(BenchmarkActivationSystem))]
    public partial struct CharacterSetupSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameState>();
            state.RequireForUpdate<MaxLevelEffect>();
            state.RequireForUpdate<Player>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<CharacterApplied>()) return;

            CharacterType type = SystemAPI.HasSingleton<BenchmarkSettings>()
                ? CharacterType.Blaster
                : SaveSystem.Current.Selected;
            CharacterDefinition character = Characters.Get(type);

            SystemAPI.GetSingletonRW<MaxLevelEffect>().ValueRW.Kind = character.Effect;

            // 피격 깜빡임이 이 색으로 되돌아오도록 BaseColor도 같이 바꾼다
            UnityEngine.Color linear = character.Color.linear;
            var color = new float4(linear.r, linear.g, linear.b, linear.a);
            Entity player = SystemAPI.GetSingletonEntity<Player>();
            state.EntityManager.SetComponentData(player, new BaseColor { Value = color });
            state.EntityManager.SetComponentData(player, new URPMaterialPropertyBaseColor { Value = color });

            // GameState와 같은 엔티티(SubScene의 Arena)에 표시 → 게임 씬을 다시 로드하면 새로 적용된다
            state.EntityManager.AddComponent<CharacterApplied>(SystemAPI.GetSingletonEntity<GameState>());
        }
    }
}
