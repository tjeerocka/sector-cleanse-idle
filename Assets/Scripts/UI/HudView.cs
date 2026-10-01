using SectorCleanse.Combat;
using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// In-round HUD, split into two side columns so it never covers the lanes:
    ///  * Left:  bank (unchanged until the round ends), money earned this round,
    ///           wave (+ best wave), $ per kill, time until the elite, time.
    ///  * Right: fighters (incl. the player), squad HP, the player's damage, the
    ///           soldiers' combined damage, fire rate and bullet speed.
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

        [Tooltip("Optional. Found automatically if left empty.")]
        [SerializeField] private WaveDirector director;

        private (int bank, int round, int wave, int best, int reward, int elite, int seconds) _shownLeft;
        private (int fighters, double hp, double player, double soldiers, float rate, float speed) _shownRight;
        private bool _hasShown;

        private void Awake()
        {
            if (!squad) squad = FindAnyObjectByType<PlayerSquad>();
            if (!weapon) weapon = FindAnyObjectByType<Weapon>();
            if (!director) director = FindAnyObjectByType<WaveDirector>();
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;

            var left = (
                bank: gm ? gm.BankedMoney : 0,
                round: gm ? gm.RoundMoney : 0,
                wave: gm ? gm.Wave : 1,
                best: gm ? gm.BestWave : 0,
                reward: gm ? gm.KillReward : 1,
                elite: director ? (director.CurrentPhase == WaveDirector.Phase.Wave
                    ? Mathf.CeilToInt(director.TimeUntilElite) : -1) : -2,
                seconds: gm ? Mathf.FloorToInt(gm.RoundTime) : 0);

            var right = (
                fighters: squad ? squad.SoldierCount : 0,
                hp: squad ? System.Math.Ceiling(squad.TotalHp) : 0d,
                player: weapon ? weapon.PlayerDamage : 0d,
                soldiers: weapon ? weapon.SoldierDamage : 0d,
                rate: weapon ? weapon.FireRate : 0f,
                speed: weapon ? weapon.BulletSpeed : 0f);

            if (leftLabel && (!_hasShown || left != _shownLeft))
            {
                leftLabel.text =
                    $"BANK ${left.bank}\nROUND +${left.round}\n" +
                    $"WAVE {left.wave}  (BEST {Mathf.Max(left.best, left.wave)})\n" +
                    $"${left.reward}/KILL\n" +
                    (left.elite >= 0 ? $"ELITE IN {left.elite}s\n" : left.elite == -1 ? "ELITE!\n" : "") +
                    $"TIME {left.seconds}s";
            }

            if (rightLabel && (!_hasShown || right != _shownRight))
            {
                rightLabel.text =
                    $"SQUAD {right.fighters}/{Barracks.FrontLineCap + 1}\n" +
                    $"SQUAD HP {NumberFormat.Short(right.hp)}\n" +
                    $"DMG {NumberFormat.Short(right.player)}\n" +
                    $"SOLDIER DMG {NumberFormat.Short(right.soldiers)}\n" +
                    $"FIRE RATE {right.rate:0.#}/s\nBULLET SPEED {right.speed:0.#}";
            }

            _shownLeft = left;
            _shownRight = right;
            _hasShown = true;
        }
    }
}
