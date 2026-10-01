using System;
using System.Collections.Generic;
using UnityEngine;

namespace SectorCleanse.Meta
{
    /// <summary>One player's line on the leaderboard (their personal bests).</summary>
    [Serializable]
    public class LeaderboardEntry
    {
        public string name;
        public int bestWave;
        public int bestRunMoney;
    }

    /// <summary>
    /// Leaderboard + name registry. Callback-based so an online implementation
    /// (Unity Gaming Services, PlayFab, own server…) can be dropped in without
    /// touching any UI. Names are compared case-insensitively.
    /// Ranking: highest wave first, then most run money, then name.
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>Claim a name. Callback gets false if it is already taken.</summary>
        void RegisterName(string name, Action<bool> callback);

        /// <summary>Report a finished run; the service keeps each player's best.</summary>
        void SubmitScore(string name, int wave, int runMoney, Action callback = null);

        /// <summary>Top <paramref name="count"/> entries, best first.</summary>
        void GetTop(int count, Action<List<LeaderboardEntry>> callback);

        /// <summary>1-based rank of a player, or 0 if not on the board (e.g. no finished run yet).</summary>
        void GetRank(string name, Action<int> callback);
    }

    /// <summary>
    /// Device-local leaderboard stored as JSON in PlayerPrefs. Every name ever
    /// registered on this device is reserved (unique per device); players only
    /// appear in the ranking once they have finished a run.
    /// Swap for an online service in <see cref="PlayerProfile.CreateLeaderboardService"/>.
    /// </summary>
    public class LocalLeaderboardService : ILeaderboardService
    {
        private const string SaveKey = "SectorCleanse.Leaderboard";

        [Serializable]
        private class SaveData
        {
            public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
        }

        private SaveData _data;

        public LocalLeaderboardService()
        {
            _data = new SaveData();
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            try
            {
                _data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboard] Resetting unreadable save: {e.Message}");
                _data = new SaveData();
            }
        }

        public void RegisterName(string name, Action<bool> callback)
        {
            if (Find(name) != null)
            {
                callback?.Invoke(false);
                return;
            }
            _data.entries.Add(new LeaderboardEntry { name = name });
            Save();
            callback?.Invoke(true);
        }

        public void SubmitScore(string name, int wave, int runMoney, Action callback = null)
        {
            LeaderboardEntry entry = Find(name);
            if (entry == null)
            {
                entry = new LeaderboardEntry { name = name };
                _data.entries.Add(entry);
            }
            entry.bestWave = Mathf.Max(entry.bestWave, wave);
            entry.bestRunMoney = Mathf.Max(entry.bestRunMoney, runMoney);
            Save();
            callback?.Invoke();
        }

        public void GetTop(int count, Action<List<LeaderboardEntry>> callback)
        {
            List<LeaderboardEntry> sorted = Sorted();
            if (sorted.Count > count) sorted.RemoveRange(count, sorted.Count - count);
            callback?.Invoke(sorted);
        }

        public void GetRank(string name, Action<int> callback)
        {
            List<LeaderboardEntry> sorted = Sorted();
            int index = sorted.FindIndex(e => SameName(e.name, name));
            callback?.Invoke(index + 1);
        }

        // ------------------------------------------------------------------

        public static bool SameName(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private LeaderboardEntry Find(string name) => _data.entries.Find(e => SameName(e.name, name));

        /// <summary>Ranked entries; registered names without a finished run are left out.</summary>
        private List<LeaderboardEntry> Sorted()
        {
            var sorted = _data.entries.FindAll(e => e.bestWave > 0 || e.bestRunMoney > 0);
            sorted.Sort((a, b) =>
            {
                int byWave = b.bestWave.CompareTo(a.bestWave);
                if (byWave != 0) return byWave;
                int byMoney = b.bestRunMoney.CompareTo(a.bestRunMoney);
                return byMoney != 0 ? byMoney : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });
            return sorted;
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
        }
    }
}
