using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// 설정값 없이 판 동안 쓰는 상태 싱글톤: 게임 상태(GameState), 통계(GameStats), 고른 증강(Augments),
    /// 연출 이벤트 큐(EffectQueue + EffectEvent 버퍼). 로직 시스템이 쓰고 UI·연출이 읽는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameSessionAuthoring : MonoBehaviour
    {
        class Baker : Baker<GameSessionAuthoring>
        {
            public override void Bake(GameSessionAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new GameState());
                AddComponent(entity, new GameStats());
                AddComponent(entity, new Augments());
                // 연출 이벤트 큐: 로직 시스템이 쓰고 EffectPresenter가 읽는다
                AddComponent(entity, new EffectQueue());
                AddBuffer<EffectEvent>(entity);
            }
        }
    }
}
