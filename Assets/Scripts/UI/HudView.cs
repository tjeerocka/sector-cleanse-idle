using SectorCleanse.Combat;
using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// In-round HUD.
    ///  * Line 1: banked money (unchanged until the round ends), money earned this
    ///    round, survival time.
    ///  * Line 2: soldiers (incl. the player) and live weapon stats, so buffs and
    ///    debuffs are visible the moment they apply.
    /// Only rewrites the text when a value actually changes.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private Text label;

        [Tooltip("Optional. Found automatically if left empty.")]
        [SerializeField] private PlayerSquad squad;

        [Tooltip("Optional. Found automatically if left empty.")]
        [SerializeField] private Weapon weapon;

        private (int bank, int round, int seconds, int soldiers, int damage, float rate, float speed) _shown;
        private bool _hasShown;

        private void Awake()
        {
            if (!squad) squad = FindAnyObjectByType<PlayerSquad>();
            if (!weapon) weapon = FindAnyObjectByType<Weapon>();
        }

        private void Update()
        {
            if (!label) return;

            GameManager gm = GameManager.Instance;
            var now = (
                bank: gm ? gm.BankedMoney : 0,
                round: gm ? gm.RoundMoney : 0,
                seconds: gm ? Mathf.FloorToInt(gm.RoundTime) : 0,
                soldiers: squad ? squad.SoldierCount : 0,
                damage: weapon ? weapon.Damage : 0,
                rate: weapon ? weapon.FireRate : 0f,
                speed: weapon ? weapon.BulletSpeed : 0f);

            if (_hasShown && now == _shown) return;
            _shown = now;
            _hasShown = true;

            label.text =
                $"BANK ${now.bank}     ROUND +${now.round}     {now.seconds}s\n" +
                $"SOLDIERS {now.soldiers}   DMG {now.damage}   RATE {now.rate:0.#}/s   BULLET {now.speed:0.#}";
        }
    }
}
