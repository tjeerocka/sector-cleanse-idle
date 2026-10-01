using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// Minimal in-round HUD: soldiers, money earned this round and survival time.
    /// Only rewrites the text when a value actually changes.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private Text label;

        [Tooltip("Optional. Found automatically if left empty.")]
        [SerializeField] private PlayerSquad squad;

        private int _lastSoldiers = -1;
        private int _lastMoney = -1;
        private int _lastSeconds = -1;

        private void Awake()
        {
            if (!squad) squad = FindAnyObjectByType<PlayerSquad>();
        }

        private void Update()
        {
            if (!label) return;

            GameManager gm = GameManager.Instance;
            int soldiers = squad ? squad.SoldierCount : 0;
            int money = gm ? gm.RoundMoney : 0;
            int seconds = gm ? Mathf.FloorToInt(gm.RoundTime) : 0;

            if (soldiers == _lastSoldiers && money == _lastMoney && seconds == _lastSeconds) return;

            _lastSoldiers = soldiers;
            _lastMoney = money;
            _lastSeconds = seconds;
            label.text = $"SOLDIERS {soldiers}      ${money}      {seconds}s";
        }
    }
}
