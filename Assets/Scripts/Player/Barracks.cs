using System;
using System.Collections.Generic;
using SectorCleanse.Core;
using UnityEngine;

namespace SectorCleanse.Player
{
    /// <summary>
    /// Permanent soldier collection, managed in the start menu.
    ///
    /// Rules:
    ///  * Soldiers have a tier T0..T100. Damage (and HP) = 5^tier: T0 = 1, T1 = 5, T2 = 25…
    ///  * Each soldier is either in RESERVE or on the FRONT LINE (deployed).
    ///  * Reserve holds at most <see cref="ReserveCap"/> of each tier.
    ///  * <see cref="ReserveCap"/> reserve soldiers of tier N merge into 1 soldier of
    ///    tier N+1 (which lands in reserve, so that tier's reserve must have room).
    ///  * At most <see cref="FrontLineCap"/> soldiers are deployed (plus the player).
    ///  * Bought T0 soldiers auto-deploy while the front line has room, else go to reserve.
    ///  * The front line is locked while a suspended run exists (those soldiers are
    ///    "out on a mission"); buying and merging reserve soldiers is still allowed.
    ///
    /// Saved as JSON in PlayerPrefs.
    /// </summary>
    [DefaultExecutionOrder(-95)] // Load before the squad / menu read it.
    [DisallowMultipleComponent]
    public class Barracks : MonoBehaviour
    {
        public const int MaxTier = 100;
        public const int ReserveCap = 5;
        public const int FrontLineCap = 10;
        private const string SaveKey = "SectorCleanse.Barracks";

        [Header("Recruit price (T0)")]
        [SerializeField, Min(0)] private int recruitBaseCost = 20;

        [Tooltip("Added to the price for every soldier bought so far.")]
        [SerializeField, Min(0)] private int recruitCostStep = 2;

        [Serializable]
        private class SaveData
        {
            public List<int> owned = new List<int>();
            public List<int> deployed = new List<int>();
            public int totalBought;
        }

        private SaveData _data = new SaveData();

        /// <summary>Raised after any change (buy, merge, deploy, return, reset).</summary>
        public event Action Changed;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            Load();
        }

        // ------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------

        /// <summary>Damage per bullet and max HP of one soldier of this tier: 5^tier.</summary>
        public static double PowerOfTier(int tier) => Math.Pow(ReserveCap, Mathf.Clamp(tier, 0, MaxTier));

        public int Owned(int tier) => Get(_data.owned, tier);
        public int Deployed(int tier) => Get(_data.deployed, tier);
        public int Reserve(int tier) => Owned(tier) - Deployed(tier);

        public int DeployedTotal
        {
            get
            {
                int total = 0;
                foreach (int count in _data.deployed) total += count;
                return total;
            }
        }

        /// <summary>Highest tier with at least one owned soldier, or -1 if none.</summary>
        public int HighestOwnedTier
        {
            get
            {
                for (int t = _data.owned.Count - 1; t >= 0; t--)
                    if (_data.owned[t] > 0) return t;
                return -1;
            }
        }

        /// <summary>The front line can't change while a suspended run is waiting to be continued.</summary>
        public bool FrontLineLocked => GameManager.Instance && GameManager.Instance.HasSavedRun;

        public int RecruitCost => recruitBaseCost + recruitCostStep * _data.totalBought;

        public bool CanRecruit =>
            Reserve(0) < ReserveCap &&
            GameManager.Instance && GameManager.Instance.BankedMoney >= RecruitCost;

        public bool CanMerge(int tier) =>
            tier >= 0 && tier < MaxTier &&
            Reserve(tier) >= ReserveCap &&
            Reserve(tier + 1) < ReserveCap;

        public bool CanDeploy(int tier) =>
            !FrontLineLocked && Reserve(tier) > 0 && DeployedTotal < FrontLineCap;

        public bool CanReturn(int tier) =>
            !FrontLineLocked && Deployed(tier) > 0 && Reserve(tier) < ReserveCap;

        /// <summary>Tiers of all deployed soldiers, strongest first (used to build the squad).</summary>
        public List<int> GetDeployedTiers()
        {
            var tiers = new List<int>();
            for (int t = _data.deployed.Count - 1; t >= 0; t--)
                for (int i = 0; i < _data.deployed[t]; i++) tiers.Add(t);
            return tiers;
        }

        // ------------------------------------------------------------------
        // Actions (each returns false when not allowed)
        // ------------------------------------------------------------------

        /// <summary>Buy one T0 soldier with banked money.</summary>
        public bool Recruit()
        {
            if (!CanRecruit || !GameManager.Instance.TrySpendBankedMoney(RecruitCost)) return false;

            Add(_data.owned, 0, 1);
            _data.totalBought++;

            // Convenience: fill empty front-line slots automatically.
            if (!FrontLineLocked && DeployedTotal < FrontLineCap) Add(_data.deployed, 0, 1);

            SaveAndNotify();
            return true;
        }

        /// <summary>Convert <see cref="ReserveCap"/> reserve soldiers of a tier into one of the next tier.</summary>
        public bool Merge(int tier)
        {
            if (!CanMerge(tier)) return false;
            Add(_data.owned, tier, -ReserveCap);
            Add(_data.owned, tier + 1, 1);
            SaveAndNotify();
            return true;
        }

        /// <summary>Move one reserve soldier of a tier to the front line.</summary>
        public bool Deploy(int tier)
        {
            if (!CanDeploy(tier)) return false;
            Add(_data.deployed, tier, 1);
            SaveAndNotify();
            return true;
        }

        /// <summary>Move one front-line soldier of a tier back to reserve.</summary>
        public bool Return(int tier)
        {
            if (!CanReturn(tier)) return false;
            Add(_data.deployed, tier, -1);
            SaveAndNotify();
            return true;
        }

        // ------------------------------------------------------------------
        // Persistence
        // ------------------------------------------------------------------

        private void Load()
        {
            _data = new SaveData();
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                MigrateLegacyRecruits();
                return;
            }

            try
            {
                _data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Barracks] Resetting unreadable save: {e.Message}");
                _data = new SaveData();
            }
        }

        /// <summary>
        /// Before the barracks existed, soldiers were a "recruit_soldier" shop upgrade.
        /// Convert those levels into T0 soldiers once (front line first, then reserve).
        /// </summary>
        private void MigrateLegacyRecruits()
        {
            const string legacyKey = "SectorCleanse.Upgrade.recruit_soldier";
            int legacy = PlayerPrefs.GetInt(legacyKey, 0);
            if (legacy <= 0) return;

            int deployed = Mathf.Min(legacy, FrontLineCap);
            int owned = Mathf.Min(legacy, FrontLineCap + ReserveCap);
            Add(_data.owned, 0, owned);
            Add(_data.deployed, 0, deployed);
            _data.totalBought = legacy;

            PlayerPrefs.DeleteKey(legacyKey);
            SaveAndNotify();
        }

        private void SaveAndNotify()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static int Get(List<int> list, int tier) =>
            tier >= 0 && tier < list.Count ? list[tier] : 0;

        private static void Add(List<int> list, int tier, int delta)
        {
            while (list.Count <= tier) list.Add(0);
            list[tier] = Mathf.Max(0, list[tier] + delta);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Add 5 T0 to reserve")]
        private void DebugAddReserve()
        {
            int room = ReserveCap - Reserve(0);
            Add(_data.owned, 0, room);
            SaveAndNotify();
        }

        [ContextMenu("Debug/Reset barracks")]
        private void DebugReset()
        {
            _data = new SaveData();
            SaveAndNotify();
        }
#endif
    }
}
