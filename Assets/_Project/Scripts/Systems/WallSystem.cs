using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 벽·문·방. 프레임 맨 앞에서 (지난 프레임 잡이 다 끝난 뒤라 기다림 없이):
    /// 1) 처음·재시작이면 문을 모두 닫고 체력을 채운다
    /// 2) 지난 프레임 총알이 문에 준 피해(WallDamage)를 반영하고, 체력이 0이 된 문은 부순다 (그리지 않음)
    /// 3) 문이 부서지면 둘레 통로를 열고(통로는 한 바퀴 이어져 있어서 전부) 부서진 문 뒤에 경험치 젬을 뿌린다
    /// 4) 서 있는 벽 목록(WallBox)·방 상태(RoomState)를 다시 만든다 → 총알·플레이어·적·스폰은 이 목록만 본다
    /// 맞은 총알이 없고 바뀐 것도 없는 프레임에는 아무것도 하지 않는다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(EffectEventCleanupSystem))]
    [BurstCompile]
    public partial struct WallSystem : ISystem
    {
        // 피해를 많이 입을수록 이 색 쪽으로
        static readonly float4 DamagedColor = new float4(1f, 0.35f, 0.2f, 1f);
        // 문이 부서질 때 문을 따라 터지는 연출 개수
        const int BreakBursts = 4;
        // 보상 젬이 문 뒤에서 퍼지는 최대 거리
        const float MaxGemSpread = 2.5f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WallSettings>();
            state.RequireForUpdate<WallDamage>();
            state.RequireForUpdate<WallBox>();
            state.RequireForUpdate<RoomState>();
            state.RequireForUpdate<EffectQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<WallDamage> damage = SystemAPI.GetSingletonRW<WallDamage>();
            float4 amount = damage.ValueRO.Amount;
            bool reset = damage.ValueRO.Dirty;
            if (math.all(amount == float4.zero) && !reset) return;
            damage.ValueRW.Amount = float4.zero;
            damage.ValueRW.Dirty = false;

            WallSettings settings = SystemAPI.GetSingleton<WallSettings>();
            DynamicBuffer<EffectEvent> effects = SystemAPI.GetSingletonBuffer<EffectEvent>();
            var structural = new EntityCommandBuffer(Allocator.Temp);
            var openedRooms = new NativeList<int>(Allocator.Temp);
            bool changed = reset;

            foreach (var (gate, block, color, baseColor, entity) in SystemAPI
                         .Query<RefRW<Gate>, RefRO<WallBlock>, RefRW<URPMaterialPropertyBaseColor>, RefRO<BaseColor>>()
                         .WithEntityAccess())
            {
                if (reset)
                {
                    gate.ValueRW.Broken = false;
                    gate.ValueRW.MaxHealth = settings.GateHealth;
                    gate.ValueRW.Health = settings.GateHealth;
                    if (SystemAPI.HasComponent<DisableRendering>(entity)) structural.RemoveComponent<DisableRendering>(entity);
                }
                if (gate.ValueRO.Broken) continue;

                if (gate.ValueRO.Index >= 0 && gate.ValueRO.Index < 4)
                    gate.ValueRW.Health -= amount[gate.ValueRO.Index];

                if (gate.ValueRO.Health <= 0f)
                {
                    gate.ValueRW.Broken = true;
                    structural.AddComponent<DisableRendering>(entity);
                    openedRooms.Add(gate.ValueRO.RoomIndex);
                    AddBreakEffects(effects, block.ValueRO, baseColor.ValueRO.Value);
                    changed = true;
                    continue;
                }

                // 체력이 줄수록 붉게 → 곧 부서진다는 걸 보여 준다
                float damaged = 1f - gate.ValueRO.Health / gate.ValueRO.MaxHealth;
                color.ValueRW.Value = math.lerp(baseColor.ValueRO.Value, DamagedColor, math.saturate(damaged));
            }

            if (changed) Rebuild(ref state, reset, openedRooms, settings);
            structural.Playback(state.EntityManager);
        }

        // 서 있는 벽(부서지지 않은 문 포함) 목록과 방 상태를 다시 만든다. 새로 열린 방에는 젬을 뿌린다
        void Rebuild(ref SystemState state, bool reset, NativeList<int> openedRooms, in WallSettings settings)
        {
            DynamicBuffer<WallBox> boxes = SystemAPI.GetSingletonBuffer<WallBox>();
            boxes.Clear();
            foreach (var (block, entity) in SystemAPI.Query<RefRO<WallBlock>>().WithEntityAccess())
            {
                int gateIndex = -1;
                if (SystemAPI.HasComponent<Gate>(entity))
                {
                    Gate gate = SystemAPI.GetComponent<Gate>(entity);
                    if (gate.Broken) continue;
                    gateIndex = gate.Index;
                }
                boxes.Add(new WallBox { Center = block.ValueRO.Center, HalfSize = block.ValueRO.HalfSize, Gate = gateIndex });
            }

            DynamicBuffer<RoomState> rooms = SystemAPI.GetSingletonBuffer<RoomState>();
            if (reset)
            {
                rooms.Clear();
                // 방 번호 = 버퍼 위치가 되도록 번호 순서대로 넣는다
                foreach (RefRO<Room> room in SystemAPI.Query<RefRO<Room>>())
                {
                    while (rooms.Length <= room.ValueRO.Index) rooms.Add(default);
                    rooms[room.ValueRO.Index] = new RoomState { Center = room.ValueRO.Center, HalfSize = room.ValueRO.HalfSize };
                }
            }

            var random = Random.CreateFromIndex((uint)(SystemAPI.Time.ElapsedTime * 1000.0) + 1u);
            var gemPositions = new NativeList<float2>(Allocator.Temp);
            if (openedRooms.Length > 0)
            {
                // 통로 조각들은 한 바퀴로 이어져 있어서, 문 하나가 뚫리면 전부 들어갈 수 있다
                for (int i = 0; i < rooms.Length; i++)
                {
                    RoomState room = rooms[i];
                    room.Open = true;
                    rooms[i] = room;
                }
            }
            foreach (int index in openedRooms)
            {
                if (index < 0 || index >= rooms.Length) continue;
                // 보상: 부서진 문 바로 뒤 (통로 조각 중심 근처, 문에서 너무 멀리 흩어지지 않게)
                RoomState room = rooms[index];
                float2 spread = math.min(room.HalfSize * 0.8f, new float2(MaxGemSpread));
                for (int i = 0; i < settings.RewardGems; i++)
                    gemPositions.Add(room.Center + random.NextFloat2(-spread, spread));
            }

            SpawnGems(ref state, settings, gemPositions);
        }

        static void AddBreakEffects(DynamicBuffer<EffectEvent> effects, in WallBlock block, float4 color)
        {
            // 문의 긴 쪽을 따라 파편
            bool wide = block.HalfSize.x >= block.HalfSize.y;
            float2 along = wide ? new float2(block.HalfSize.x, 0f) : new float2(0f, block.HalfSize.y);
            for (int i = 0; i < BreakBursts; i++)
            {
                float t = (i + 0.5f) / BreakBursts * 2f - 1f;
                effects.Add(new EffectEvent
                {
                    Type = EffectType.Explosion,
                    Position = block.Center + along * t,
                    Size = 0.8f,
                    Color = color,
                });
            }
        }

        // 문이 부서지는 건 드물어서 그때만 구조 변경(젬 생성)을 바로 한다
        void SpawnGems(ref SystemState state, in WallSettings settings, NativeList<float2> positions)
        {
            if (positions.Length == 0 || settings.GemPrefab == Entity.Null) return;

            LocalTransform prefabTransform = SystemAPI.GetComponent<LocalTransform>(settings.GemPrefab);
            NativeArray<Entity> gems = state.EntityManager.Instantiate(settings.GemPrefab, positions.Length, Allocator.Temp);
            for (int i = 0; i < gems.Length; i++)
            {
                LocalTransform gemTransform = prefabTransform;
                gemTransform.Position = new float3(positions[i], prefabTransform.Position.z);
                gemTransform.Rotation = quaternion.RotateZ(math.PI * 0.25f); // 적이 떨어뜨리는 젬과 같은 마름모
                state.EntityManager.SetComponentData(gems[i], gemTransform);
                state.EntityManager.SetComponentData(gems[i], new XpGem { Value = settings.GemValue });
            }
        }
    }
}
