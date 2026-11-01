using MergeBullet.Components;
using MergeBullet.Meta;
using Unity.Entities;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 판 밖(저장 데이터)과 판 안(ECS)을 잇는 다리. 게임 씬이 로드되어 GameState가 생기면 한 번,
    /// 저장된 영구 강화 단계로 PermanentBonus 싱글톤을 만든다. ProgressionSystem은 이게 있어야 기본 능력치를 기록한다.
    /// 벤치마크 중에는 저장 상태에 따라 측정 조건이 달라지지 않도록 보너스 없이 만든다.
    /// 저장 데이터는 관리 코드라 Burst를 쓰지 않는다 (게임 씬 진입 때 한 번만 실행).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(BenchmarkActivationSystem))]
    [UpdateBefore(typeof(ProgressionSystem))]
    public partial struct PermanentBonusSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<PermanentBonus>()) return;

            PermanentBonus bonus = SystemAPI.HasSingleton<BenchmarkSettings>()
                ? PermanentBonus.None
                : PermanentUpgrades.BonusFor(SaveSystem.Current);

            // GameState와 같은 엔티티(SubScene의 Arena)에 붙인다 → 게임 씬을 떠나면 함께 사라지고, 다시 오면 새로 계산된다
            state.EntityManager.AddComponentData(SystemAPI.GetSingletonEntity<GameState>(), bonus);
        }
    }
}
