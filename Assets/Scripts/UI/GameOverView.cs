using SectorCleanse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// Game-over overlay summary: money earned this round, wave reached, survival
    /// time and the new bank total. GameManager stores the result before activating
    /// this overlay, so reading it in OnEnable is always up to date.
    /// </summary>
    public class GameOverView : MonoBehaviour
    {
        [SerializeField] private Text summaryLabel;

        private void OnEnable()
        {
            if (!summaryLabel || !GameManager.Instance) return;

            GameManager gm = GameManager.Instance;
            RoundResult result = gm.LastRoundResult;
            summaryLabel.text =
                $"+${result.MoneyEarned} EARNED\n" +
                $"REACHED WAVE {result.Wave} ({Mathf.FloorToInt(result.SurvivalTime)}s)\n" +
                $"BANK ${gm.BankedMoney}";
        }
    }
}
