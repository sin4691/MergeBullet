using System;
using System.IO;
using MergeBullet.Components;
using UnityEngine;

namespace MergeBullet.Meta
{
    /// <summary>판과 판 사이에 남는 데이터. JSON으로 저장한다.</summary>
    [Serializable]
    public class SaveData
    {
        public int Coins;
        public int RunsPlayed;
        public float BestSurvivalTime;
        public int BestKills;
        /// <summary>저장 형식 버전. 형식이 바뀌면 올리고, 불러올 때 예전 형식을 옮긴다 (SaveSystem.Migrate).</summary>
        public int Version = SaveSystem.CurrentVersion;
        /// <summary>영구 강화 단계. 인덱스 = UpgradeType. 길이가 모자라면(강화 종류가 늘었을 때) 늘려 쓴다.</summary>
        public int[] Upgrades = new int[PermanentUpgrades.Count];
        /// <summary>버전 1의 영구 강화 단계 (순서: 연사·체력·피해·이동·자석). 불러올 때 Upgrades로 옮기고 비운다.</summary>
        public int[] UpgradeLevels;
        /// <summary>선택한 캐릭터 (CharacterType).</summary>
        public int SelectedCharacter;
        /// <summary>해금한 캐릭터 비트 (1 &lt;&lt; CharacterType). 기본 캐릭터(Blaster)는 처음부터 해금.</summary>
        public int UnlockedCharacters = 1;
        /// <summary>고른 언어 코드 (en, ko). 비어 있으면 OS 언어로 시작한다.</summary>
        public string Language = "";
        /// <summary>전체 음량 0~1 (GameSettings). 예전 저장 파일에는 없어서 기본값 1로 시작한다.</summary>
        public float Volume = 1f;
        /// <summary>배경음악 음량 0~1 (전체 음량에 곱해진다). 효과음이 묻히지 않게 기본은 조금 낮게.</summary>
        public float MusicVolume = 0.6f;
        /// <summary>제한 시간까지 버틴 판 수.</summary>
        public int RunsCleared;

        public bool IsUnlocked(CharacterType type) => (UnlockedCharacters & (1 << (int)type)) != 0;

        // 저장 파일이 손상돼 잘못된 값이 들어 있어도 해금된 캐릭터만 고르게 한다
        public CharacterType Selected
            => SelectedCharacter >= 0 && SelectedCharacter < Characters.Count && IsUnlocked((CharacterType)SelectedCharacter)
                ? (CharacterType)SelectedCharacter
                : CharacterType.Blaster;

        public int GetUpgradeLevel(UpgradeType type)
            => Upgrades != null && (int)type < Upgrades.Length ? Upgrades[(int)type] : 0;

        public void SetUpgradeLevel(UpgradeType type, int level)
        {
            if (Upgrades == null || Upgrades.Length < PermanentUpgrades.Count)
                Array.Resize(ref Upgrades, PermanentUpgrades.Count);
            Upgrades[(int)type] = level;
        }
    }

    /// <summary>한 판이 끝났을 때 받은 보상과 갱신된 기록.</summary>
    public readonly struct RunReward
    {
        /// <summary>받은 코인 전부 (클리어 보너스 포함).</summary>
        public readonly int Coins;
        public readonly int ClearBonus;
        public readonly bool NewBestTime;
        public readonly bool NewBestKills;

        public RunReward(int coins, int clearBonus, bool newBestTime, bool newBestKills)
        {
            Coins = coins;
            ClearBonus = clearBonus;
            NewBestTime = newBestTime;
            NewBestKills = newBestKills;
        }
    }

    /// <summary>
    /// 저장 데이터를 불러오고 쓴다 (persistentDataPath/save.json).
    /// 판 안의 게임 로직은 ECS, 판 밖에 남는 데이터(코인·기록)는 여기서 관리 코드로 다룬다.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>저장 형식 버전. 2: 영구 강화를 판 안 강화와 같은 목록(UpgradeType)으로 통일</summary>
        public const int CurrentVersion = 2;

        // 버전 1의 영구 강화 배열 순서 → 지금의 UpgradeType
        static readonly UpgradeType[] Version1UpgradeOrder =
        {
            UpgradeType.FireRate, UpgradeType.MaxHealth, UpgradeType.Damage, UpgradeType.MoveSpeed, UpgradeType.Magnet,
        };

        /// <summary>예전 형식을 지금 형식으로 옮긴다. 옮겼으면 true (바로 다시 저장).</summary>
        static bool Migrate(SaveData data)
        {
            if (data.Version >= CurrentVersion) return false;

            if (data.Version < 2)
            {
                // 산 강화 단계를 새 순서의 같은 강화로 옮긴다 (코인을 쓴 만큼 그대로 남게)
                data.Upgrades = new int[PermanentUpgrades.Count];
                if (data.UpgradeLevels != null)
                    for (int i = 0; i < data.UpgradeLevels.Length && i < Version1UpgradeOrder.Length; i++)
                        data.Upgrades[(int)Version1UpgradeOrder[i]] = data.UpgradeLevels[i];
                data.UpgradeLevels = null;
            }

            data.Version = CurrentVersion;
            return true;
        }

        // 생존 시간 이 초마다 코인 1개 (+ 처치 1마리당 코인 1개)
        public const float SecondsPerCoin = 5f;
        // 보스 1마리당 추가 코인
        public const int CoinsPerBoss = 20;
        // 제한 시간까지 버티면 추가 코인 (30분 생존만으로 약 360개라 그 절반 남짓)
        public const int ClearBonus = 200;

        static SaveData _current;

        static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static SaveData Current => _current ??= Load();

        public static int CoinsFor(GameStats stats)
            => stats.Kills + Mathf.FloorToInt(stats.SurvivalTime / SecondsPerCoin) + stats.BossKills * CoinsPerBoss;

        /// <summary>
        /// 끝난 판의 코인을 더하고 최고 기록을 갱신한 뒤 저장한다. 죽었을 때·클리어했을 때·도중에 로비로 나갈 때 모두
        /// 지금까지 번 만큼 받는다 (클리어면 보너스 추가).
        /// </summary>
        public static RunReward AwardRun(GameStats stats, bool cleared)
        {
            SaveData data = Current;
            int bonus = cleared ? ClearBonus : 0;
            int coins = CoinsFor(stats) + bonus;
            bool newBestTime = stats.SurvivalTime > data.BestSurvivalTime;
            bool newBestKills = stats.Kills > data.BestKills;

            data.Coins += coins;
            data.RunsPlayed++;
            if (cleared) data.RunsCleared++;
            if (newBestTime) data.BestSurvivalTime = stats.SurvivalTime;
            if (newBestKills) data.BestKills = stats.Kills;
            Save();

            return new RunReward(coins, bonus, newBestTime, newBestKills);
        }

        /// <summary>코인이 충분하고 최대 단계가 아니면 한 단계 사고 저장한다.</summary>
        public static bool TryBuyUpgrade(UpgradeType type)
        {
            SaveData data = Current;
            int level = data.GetUpgradeLevel(type);
            if (level >= PermanentUpgrades.Get(type).MaxLevel) return false;

            int cost = PermanentUpgrades.CostOf(type, level);
            if (data.Coins < cost) return false;

            data.Coins -= cost;
            data.SetUpgradeLevel(type, level + 1);
            Save();
            return true;
        }

        /// <summary>코인이 충분하면 캐릭터를 해금하고 바로 선택한 뒤 저장한다.</summary>
        public static bool TryUnlockCharacter(CharacterType type)
        {
            SaveData data = Current;
            if (data.IsUnlocked(type)) return false;

            int cost = Characters.Get(type).UnlockCost;
            if (data.Coins < cost) return false;

            data.Coins -= cost;
            data.UnlockedCharacters |= 1 << (int)type;
            data.SelectedCharacter = (int)type;
            Save();
            return true;
        }

        public static void SelectCharacter(CharacterType type)
        {
            if (!Current.IsUnlocked(type)) return;
            Current.SelectedCharacter = (int)type;
            Save();
        }

        public static void Save() => Write(Current);

        static void Write(SaveData data)
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[Save] could not write {FilePath}: {e.Message}");
            }
        }

        // 파일이 없거나 깨졌으면 새 데이터로 시작한다 (저장 실패로 게임이 멈추지 않게)
        static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    // 버전 필드가 없는 예전 파일은 0으로 남도록, 0으로 둔 객체 위에 덮어쓴다
                    // (FromJson은 새 객체의 기본값(최신 버전)을 그대로 남겨서 예전 파일을 구분할 수 없다)
                    var data = new SaveData { Version = 0 };
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), data);
                    if (Migrate(data)) Write(data);
                    return data;
                }
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                Debug.LogWarning($"[Save] could not read {FilePath}, starting fresh: {e.Message}");
            }
            return new SaveData();
        }
    }
}
