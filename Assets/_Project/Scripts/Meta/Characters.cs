using MergeBullet.Components;
using UnityEngine;

namespace MergeBullet.Meta
{
    /// <summary>플레이어 캐릭터. 저장 데이터의 선택값·해금 비트로도 쓰므로 순서를 바꾸지 않는다.</summary>
    public enum CharacterType
    {
        Blaster,
        Storm,
    }

    /// <summary>
    /// 캐릭터 하나의 정의. 캐릭터마다 레벨 4 특수효과와 플레이어 색이 다르다.
    /// 이름·설명은 화면 문구라 여기 두지 않고 UI 문자열 표(character.{종류}.name/desc)에 둔다.
    /// </summary>
    public readonly struct CharacterDefinition
    {
        public readonly MaxLevelEffectKind Effect;
        public readonly int UnlockCost;
        public readonly Color Color;

        public CharacterDefinition(MaxLevelEffectKind effect, int unlockCost, Color color)
        {
            Effect = effect;
            UnlockCost = unlockCost;
            Color = color;
        }
    }

    /// <summary>캐릭터 표. 로비(선택·해금 표시)와 게임 시작(효과·색 적용)이 같은 표를 쓴다.</summary>
    public static class Characters
    {
        public const int Count = 2;

        static readonly CharacterDefinition[] Table =
        {
            new CharacterDefinition(MaxLevelEffectKind.Explosion, // 레벨 4 총알이 폭발
                unlockCost: 0, new Color(0.3f, 0.75f, 1f)),
            new CharacterDefinition(MaxLevelEffectKind.Lightning, // 레벨 4 총알이 연쇄 번개
                unlockCost: 100, new Color(0.2f, 0.95f, 0.85f)),
        };

        public static CharacterDefinition Get(CharacterType type) => Table[(int)type];
    }
}
