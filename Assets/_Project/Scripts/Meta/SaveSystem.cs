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
    }

    /// <summary>한 판이 끝났을 때 받은 보상과 갱신된 기록.</summary>
    public readonly struct RunReward
    {
        public readonly int Coins;
        public readonly bool NewBestTime;
        public readonly bool NewBestKills;

        public RunReward(int coins, bool newBestTime, bool newBestKills)
        {
            Coins = coins;
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
        // 생존 시간 이 초마다 코인 1개 (+ 처치 1마리당 코인 1개)
        public const float SecondsPerCoin = 5f;

        static SaveData _current;

        static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static SaveData Current => _current ??= Load();

        public static int CoinsFor(GameStats stats) => stats.Kills + Mathf.FloorToInt(stats.SurvivalTime / SecondsPerCoin);

        /// <summary>끝난 판의 코인을 더하고 최고 기록을 갱신한 뒤 저장한다.</summary>
        public static RunReward AwardRun(GameStats stats)
        {
            SaveData data = Current;
            int coins = CoinsFor(stats);
            bool newBestTime = stats.SurvivalTime > data.BestSurvivalTime;
            bool newBestKills = stats.Kills > data.BestKills;

            data.Coins += coins;
            data.RunsPlayed++;
            if (newBestTime) data.BestSurvivalTime = stats.SurvivalTime;
            if (newBestKills) data.BestKills = stats.Kills;
            Save();

            return new RunReward(coins, newBestTime, newBestKills);
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
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
                    return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData();
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                Debug.LogWarning($"[Save] could not read {FilePath}, starting fresh: {e.Message}");
            }
            return new SaveData();
        }
    }
}
