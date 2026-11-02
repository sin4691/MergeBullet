using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 휴대폰 가상 조이스틱(TouchJoystick, UI)이 쓰는 이동 값. UI는 ECS를 직접 건드리지 않고 여기에만 쓰고,
    /// PlayerInputSystem이 키보드·게임패드와 똑같이 읽어 간다. 메인 스레드에서만 쓰고 읽는다.
    /// </summary>
    public static class VirtualStickInput
    {
        public static float2 Move;
    }

    /// <summary>
    /// 키보드(WASD/방향키)·게임패드·가상 조이스틱 입력을 PlayerMoveInput 컴포넌트로 옮긴다.
    /// Input System은 관리 코드(managed)라 Burst를 쓸 수 없어서, 입력 읽기만 이 시스템에 모으고
    /// 나머지 로직(이동·발사)은 Burst 시스템이 PlayerMoveInput 데이터만 보고 처리한다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct PlayerInputSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMoveInput>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float2 move = float2.zero;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
                move += (float2)gamepad.leftStick.ReadValue();

            move += VirtualStickInput.Move;

            // 대각선이 더 빠르지 않도록 길이를 1로 제한 (스틱의 살짝 기울임은 유지)
            float length = math.length(move);
            if (length > 1f) move /= length;

            SystemAPI.SetSingleton(new PlayerMoveInput { Move = move });
        }
    }
}
