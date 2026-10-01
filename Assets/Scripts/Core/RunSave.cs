using System;
using System.Collections.Generic;
using UnityEngine;

namespace SectorCleanse.Core
{
    /// <summary>Snapshot of an in-progress run, written when the app is paused/closed or the run is suspended.</summary>
    [Serializable]
    public class RunSaveData
    {
        public int wave;          // 0 in saves from before elites (derive from roundTime).
        public float roundTime;   // Total seconds played (display only).
        public float waveTime;    // Progress through the current wave's normal phase.
        public int roundMoney;
        public double playerHp;
        public float playerX;
        public int shields;
        public List<SavedSoldier> soldiers = new List<SavedSoldier>();
        public List<SavedEffect> effects = new List<SavedEffect>();
    }

    /// <summary>An active buff/debuff: its type (as int, see Combat.EffectType) and seconds left.</summary>
    [Serializable]
    public struct SavedEffect
    {
        public int type;
        public float remaining;
        public float duration;

        public SavedEffect(int type, float remaining, float duration)
        {
            this.type = type;
            this.remaining = remaining;
            this.duration = duration;
        }
    }

    [Serializable]
    public struct SavedSoldier
    {
        public int tier;
        public double hp;

        public SavedSoldier(int tier, double hp)
        {
            this.tier = tier;
            this.hp = hp;
        }
    }

    /// <summary>
    /// Implemented by gameplay components that own part of the run state.
    /// They register with <see cref="RunSave"/> while enabled; GameManager asks every
    /// registered component to write its state when saving and to restore it when a
    /// saved run is continued (after the normal RoundStarted reset).
    /// </summary>
    public interface IRunStatePersistent
    {
        void SaveRunState(RunSaveData data);
        void LoadRunState(RunSaveData data);
    }

    /// <summary>Storage for the single saved run (JSON in PlayerPrefs) plus the persistent-state registry.</summary>
    public static class RunSave
    {
        private const string Key = "SectorCleanse.RunSave";

        private static readonly List<IRunStatePersistent> Participants = new List<IRunStatePersistent>();

        public static IReadOnlyList<IRunStatePersistent> Registered => Participants;

        public static bool Exists => PlayerPrefs.HasKey(Key);

        public static void Register(IRunStatePersistent participant)
        {
            if (!Participants.Contains(participant)) Participants.Add(participant);
        }

        public static void Unregister(IRunStatePersistent participant) => Participants.Remove(participant);

        public static void Write(RunSaveData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        /// <summary>The saved run, or null if there is none (or it is unreadable).</summary>
        public static RunSaveData Read()
        {
            if (!Exists) return null;
            try
            {
                return JsonUtility.FromJson<RunSaveData>(PlayerPrefs.GetString(Key));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] Discarding unreadable save: {e.Message}");
                Delete();
                return null;
            }
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
