using SectorCleanse.Combat;
using SectorCleanse.Core;
using SectorCleanse.Meta;
using SectorCleanse.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// Start-menu info: banked money and the stats you'll deploy with.
    /// Refreshes the stats after every shop purchase.
    /// </summary>
    public class MenuView : MonoBehaviour
    {
        [SerializeField] private Text bankLabel;
        [SerializeField] private Text statsLabel;

        [Tooltip("Optional. Found automatically if left empty (also when inactive).")]
        [SerializeField] private Weapon weapon;

        [Tooltip("Optional. Found automatically if left empty (also when inactive).")]
        [SerializeField] private PlayerSquad squad;

        [Tooltip("Optional. Found automatically if left empty.")]
        [SerializeField] private UpgradeShop shop;

        private int _shownBank = -1;

        private void Awake()
        {
            // The player lives under GameplayRoot, which is inactive while the menu is up.
            if (!weapon) weapon = FindAnyObjectByType<Weapon>(FindObjectsInactive.Include);
            if (!squad) squad = FindAnyObjectByType<PlayerSquad>(FindObjectsInactive.Include);
            if (!shop) shop = FindAnyObjectByType<UpgradeShop>();
        }

        private void OnEnable()
        {
            if (shop) shop.UpgradesChanged += RefreshStats;
            _shownBank = -1;
            RefreshStats();
        }

        private void OnDisable()
        {
            if (shop) shop.UpgradesChanged -= RefreshStats;
        }

        private void Update()
        {
            if (!bankLabel || !GameManager.Instance) return;

            int bank = GameManager.Instance.BankedMoney;
            if (bank == _shownBank) return;
            _shownBank = bank;
            bankLabel.text = $"BANK ${bank}";
        }

        /// <summary>Call after buying an upgrade so the menu shows the new values.</summary>
        public void RefreshStats()
        {
            if (!statsLabel) return;

            string weaponStats = weapon
                ? $"DMG {weapon.Damage}   RATE {weapon.FireRate:0.#}/s   BULLET {weapon.BulletSpeed:0.#}"
                : "";
            string squadStats = squad ? $"STARTING SOLDIERS {squad.StartingSoldiers}" : "";
            statsLabel.text = $"{weaponStats}\n{squadStats}";
        }
    }
}
