using SectorCleanse.Combat;
using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// In-round HUD, split into two side columns so it never covers the lanes:
    ///  * Left:  bank (unchanged until the round ends), money earned this round, wave, time.
    ///  * Right: fighters (incl. the player), squad HP and live weapon stats.
    /// Only rewrites text when a value actually changes.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private Text leftLabel;
        [SerializeField] private Text rightLabel;

        [Tooltip("Optional. Found automatically if left empty.")]
        [SerializeField] private PlayerSquad squad;

        [Tooltip("Optional. Found automatically if left empty.")]
        [SerializeField] private Weapon weapon;

        private (int bank, int round, int wave, int seconds) _shownLeft;
        private (int fighters, double hp, double volley, float rate, float speed) _shownRight;
        private bool _hasShown;

        private void Awake()
        {
            if (!squad) squad = FindAnyObjectByType<PlayerSquad>();
            if (!weapon) weapon = FindAnyObjectByType<Weapon>();
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;

            var left = (
                bank: gm ? gm.BankedMoney : 0,
                round: gm ? gm.RoundMoney : 0,
                wave: gm ? gm.Wave : 1,
                seconds: gm ? Mathf.FloorToInt(gm.RoundTime) : 0);

            var right = (
                fighters: squad ? squad.SoldierCount : 0,
                hp: squad ? System.Math.Ceiling(squad.TotalHp) : 0d,
                volley: weapon ? weapon.VolleyDamage : 0d,
                rate: weapon ? weapon.FireRate : 0f,
                speed: weapon ? weapon.BulletSpeed : 0f);

            if (leftLabel && (!_hasShown || left != _shownLeft))
            {
                leftLabel.text =
                    $"BANK ${left.bank}\nROUND +${left.round}\nWAVE {left.wave}\nTIME {left.seconds}s";
            }

            if (rightLabel && (!_hasShown || right != _shownRight))
            {
                rightLabel.text =
                    $"SQUAD {right.fighters}/{Barracks.FrontLineCap + 1}\n" +
                    $"SQUAD HP {NumberFormat.Short(right.hp)}\n" +
                    $"VOLLEY DMG {NumberFormat.Short(right.volley)}\n" +
                    $"FIRE RATE {right.rate:0.#}/s\nBULLET SPEED {right.speed:0.#}";
            }

            _shownLeft = left;
            _shownRight = right;
            _hasShown = true;
        }
    }
}
