using SectorCleanse.Core;
using SectorCleanse.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// Game-over overlay summary: money earned this round, wave reached, survival
    /// time, the new bank total and a NEW HIGHSCORE line when a best was beaten.
    /// GameManager stores the result before activating this overlay, so reading it
    /// in OnEnable is always up to date.
    /// </summary>
    public class GameOverView : MonoBehaviour
    {
        [SerializeField] private Text summaryLabel;
        [SerializeField] private PlayerProfile profile;

        private void Awake()
        {
            if (!profile) profile = FindAnyObjectByType<PlayerProfile>();
        }

        private void OnEnable()
        {
            if (!summaryLabel || !GameManager.Instance) return;

            GameManager gm = GameManager.Instance;
            RoundResult result = gm.LastRoundResult;
            string highscore = gm.LastRoundWasHighscore ? "NEW HIGHSCORE!\n" : "";
            summaryLabel.text =
                highscore +
                $"+${result.MoneyEarned} EARNED\n" +
                $"REACHED WAVE {result.Wave} ({Mathf.FloorToInt(result.SurvivalTime)}s)\n" +
                $"BANK ${gm.BankedMoney}";

            // Leaderboard placement (may arrive later for an online board).
            string baseText = summaryLabel.text;
            if (profile) profile.GetOwnRank(rank =>
            {
                if (rank > 0 && summaryLabel) summaryLabel.text = baseText + $"\nLEADERBOARD #{rank}";
            });
        }
    }
}
