using System;
using System.Collections.Generic;
using SectorCleanse.Combat;
using SectorCleanse.Core;
using UnityEngine;

namespace SectorCleanse.Meta
{
    /// <summary>
    /// Permanent upgrades bought in the start menu with banked money.
    ///
    /// Each upgrade has a level (saved in PlayerPrefs) and a cost that grows by a
    /// fixed step per level. Bought levels are applied to gameplay components and
    /// take effect from the next round.
    ///
    /// Upgrades: Damage (+1 per bullet), Fire Rate (+0.5 shots/s).
    /// Soldiers are bought in the Barracks instead. New upgrades = a list entry +
    /// a line in <see cref="ApplyAll"/>.
    /// </summary>
    [DefaultExecutionOrder(-90)] // Load levels before menu UI reads them.
    [DisallowMultipleComponent]
    public class UpgradeShop : MonoBehaviour
    {
        public const string DamageId = "damage";
        public const string FireRateId = "fire_rate";
        private const string SaveKeyPrefix = "SectorCleanse.Upgrade.";

        [Serializable]
        public class Upgrade
        {
            [Tooltip("Stable save key. Don't rename after release or players lose progress.")]
            public string id;
            public string displayName;
            [Tooltip("Short effect text shown on the shop button.")]
            public string description;
            [Min(0)] public int baseCost = 25;
            [Tooltip("Added to the cost for every level already owned.")]
            [Min(0)] public int costStep = 25;
            [Tooltip("0 = unlimited.")]
            [Min(0)] public int maxLevel;
        }

        [Tooltip("Optional. Found automatically if left empty (also when inactive).")]
        [SerializeField] private Weapon weapon;

        [Header("Effect per level")]
        [SerializeField, Min(0)] private int damagePerLevel = 1;
        [SerializeField, Min(0f)] private float fireRatePerLevel = 0.5f;

        [SerializeField] private List<Upgrade> upgrades = new List<Upgrade>
        {
            new Upgrade { id = DamageId, displayName = "DAMAGE", description = "+1 DMG PER BULLET",
                baseCost = 40, costStep = 40 },
            new Upgrade { id = FireRateId, displayName = "FIRE RATE", description = "+0.5 SHOTS/S",
                baseCost = 30, costStep = 30, maxLevel = 20 },
        };

        /// <summary>Raised after any purchase or reset.</summary>
        public event Action UpgradesChanged;

        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (!weapon) weapon = FindAnyObjectByType<Weapon>(FindObjectsInactive.Include);

            foreach (Upgrade upgrade in upgrades)
                _levels[upgrade.id] = PlayerPrefs.GetInt(SaveKeyPrefix + upgrade.id, 0);

            ApplyAll();
        }

        // ------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------

        public Upgrade Find(string id) => upgrades.Find(u => u.id == id);

        public int GetLevel(string id) => _levels.TryGetValue(id, out int level) ? level : 0;

        public bool IsMaxed(string id)
        {
            Upgrade upgrade = Find(id);
            return upgrade != null && upgrade.maxLevel > 0 && GetLevel(id) >= upgrade.maxLevel;
        }

        public int GetCost(string id)
        {
            Upgrade upgrade = Find(id);
            return upgrade == null ? 0 : upgrade.baseCost + upgrade.costStep * GetLevel(id);
        }

        public bool CanAfford(string id) =>
            GameManager.Instance && GameManager.Instance.BankedMoney >= GetCost(id);

        // ------------------------------------------------------------------
        // Actions
        // ------------------------------------------------------------------

        /// <summary>Spend banked money on one level. Returns false if unknown, maxed or unaffordable.</summary>
        public bool TryBuy(string id)
        {
            if (Find(id) == null || IsMaxed(id) || !GameManager.Instance) return false;
            if (!GameManager.Instance.TrySpendBankedMoney(GetCost(id))) return false;

            SetLevel(id, GetLevel(id) + 1);
            return true;
        }

        private void SetLevel(string id, int level)
        {
            _levels[id] = level;
            PlayerPrefs.SetInt(SaveKeyPrefix + id, level);
            PlayerPrefs.Save();

            ApplyAll();
            UpgradesChanged?.Invoke();
        }

        /// <summary>Push bought levels into the gameplay components.</summary>
        private void ApplyAll()
        {
            if (weapon)
            {
                weapon.SetUpgradeBonuses(
                    GetLevel(DamageId) * damagePerLevel,
                    GetLevel(FireRateId) * fireRatePerLevel);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Reset all upgrades")]
        private void DebugResetUpgrades()
        {
            foreach (Upgrade upgrade in upgrades) SetLevel(upgrade.id, 0);
        }
#endif
    }
}
