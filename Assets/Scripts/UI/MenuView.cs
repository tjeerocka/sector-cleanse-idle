using SectorCleanse.Combat;
using SectorCleanse.Core;
using SectorCleanse.Meta;
using SectorCleanse.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// Start-menu info and run buttons:
    ///  * bank total, highscores, the wave the next run starts at (checkpoint) and
    ///    the stats of the front line you'll deploy with;
    ///  * DEPLOY when no run is saved, otherwise CONTINUE + ABANDON with the saved
    ///    run's wave and money.
    /// Refreshes after shop purchases, barracks changes and save changes.
    /// </summary>
    public class MenuView : MonoBehaviour
    {
        [SerializeField] private Text bankLabel;
        [SerializeField] private Text statsLabel;

        [Header("Run buttons (swapped depending on whether a run is saved)")]
        [SerializeField] private GameObject deployButton;
        [SerializeField] private GameObject continueButton;
        [SerializeField] private GameObject abandonButton;

        [Header("Optional, found automatically if left empty")]
        [SerializeField] private Weapon weapon;
        [SerializeField] private UpgradeShop shop;
        [SerializeField] private Barracks barracks;

        private int _shownBank = -1;

        private void Awake()
        {
            // The player lives under GameplayRoot, which is inactive while the menu is up.
            if (!weapon) weapon = FindAnyObjectByType<Weapon>(FindObjectsInactive.Include);
            if (!shop) shop = FindAnyObjectByType<UpgradeShop>();
            if (!barracks) barracks = FindAnyObjectByType<Barracks>();
        }

        private void OnEnable()
        {
            if (shop) shop.UpgradesChanged += Refresh;
            if (barracks) barracks.Changed += Refresh;
            if (GameManager.Instance) GameManager.Instance.SavedRunChanged += Refresh;
            _shownBank = -1;
            Refresh();
        }

        private void OnDisable()
        {
            if (shop) shop.UpgradesChanged -= Refresh;
            if (barracks) barracks.Changed -= Refresh;
            if (GameManager.Instance) GameManager.Instance.SavedRunChanged -= Refresh;
        }

        private void Update()
        {
            if (!bankLabel || !GameManager.Instance) return;

            int bank = GameManager.Instance.BankedMoney;
            if (bank == _shownBank) return;
            _shownBank = bank;
            bankLabel.text = $"BANK ${bank}";
        }

        /// <summary>Re-read stats and saved-run state.</summary>
        public void Refresh()
        {
            RunSaveData saved = RunSave.Read();
            bool hasRun = saved != null;

            if (deployButton) deployButton.SetActive(!hasRun);
            if (continueButton) continueButton.SetActive(hasRun);
            if (abandonButton) abandonButton.SetActive(hasRun);

            if (!statsLabel) return;

            GameManager gm = GameManager.Instance;
            int deployed = barracks ? barracks.DeployedTotal : 0;
            string playerDmg = weapon ? NumberFormat.Short(weapon.PlayerDamage) : "-";
            string soldierDmg = weapon && barracks
                ? NumberFormat.Short(weapon.EstimateSoldierDamage(barracks.GetDeployedTiers()))
                : "-";
            string rate = weapon ? $"{weapon.FireRate:0.#}/s" : "-";

            string highscore = gm ? $"HIGHSCORE: WAVE {gm.BestWave}   BEST RUN ${gm.BestRunMoney}" : "";
            string runLine = hasRun
                ? $"RUN IN PROGRESS: WAVE {(gm ? gm.WaveAt(saved.roundTime) : 1)}, +${saved.roundMoney}, {saved.soldiers.Count} SOLDIERS LEFT"
                : $"NEXT RUN STARTS AT WAVE {(gm ? gm.StartWave : 1)} (CHECKPOINT EVERY {(gm ? gm.CheckpointEvery : 5)} WAVES)";

            statsLabel.text =
                $"{highscore}\n{runLine}\n" +
                $"FRONT LINE {deployed}/{Barracks.FrontLineCap}   DMG {playerDmg}   SOLDIER DMG {soldierDmg}   FIRE RATE {rate}";
        }
    }
}
