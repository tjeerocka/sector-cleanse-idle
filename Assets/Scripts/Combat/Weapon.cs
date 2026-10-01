using System.Collections.Generic;
using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;

namespace SectorCleanse.Combat
{
    /// <summary>
    /// Auto-fires bullets straight up while a round is running: one from the player
    /// and, if a <see cref="SquadFormation"/> is present, one from every soldier in
    /// the same volley. Each bullet carries its shooter's damage.
    ///
    /// Bullet damage: (unit base + permanent shop bonus) × buff multiplier × manual boost.
    ///  * Unit base: the player's base damage, or 5^tier for a soldier.
    ///  * Shop bonus: set by the UpgradeShop via <see cref="SetUpgradeBonuses"/>.
    ///  * Multipliers: buffs/debuffs (set by RunEffects); reset each round.
    ///  * Manual boost: while the FIRE button is held (<see cref="ManualFire"/>), the
    ///    whole squad fires faster and harder, and its bullets are tagged "manual"
    ///    (they pay more and can hurt elites).
    /// </summary>
    [DisallowMultipleComponent]
    public class Weapon : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional. Falls back to LaneSystem.Instance. Used to know when bullets leave the play area.")]
        [SerializeField] private LaneSystem laneSystem;

        [Header("Base stats (shop upgrades add on top)")]
        [SerializeField, Min(1)] private int baseDamage = 1;

        [Tooltip("Shots per second.")]
        [SerializeField, Min(0.1f)] private float baseFireRate = 4f;

        [Header("Bullets")]
        [SerializeField, Min(0.1f)] private float bulletSpeed = 14f;
        [SerializeField] private Vector2 bulletSize = new Vector2(0.15f, 0.4f);
        [SerializeField] private Sprite bulletSprite;
        [SerializeField] private Color bulletColor = new Color(1f, 0.9f, 0.3f);
        [SerializeField] private Color manualBulletColor = new Color(0.4f, 0.95f, 1f);

        [Tooltip("Max horizontal distance between a soldier's bullet and the player's column.")]
        [SerializeField, Min(0f)] private float soldierBulletSpread = 0.3f;

        /// <summary>Temporary damage modifier (buffs). Reset to 1 each round.</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>Temporary fire-rate modifier (buffs / "Lowered Fire Rate"). Reset to 1 each round.</summary>
        public float FireRateMultiplier { get; set; } = 1f;

        /// <summary>No firing at all while true ("Jammed" debuff).</summary>
        public bool Suppressed { get; set; }

        /// <summary>The hold-to-fire boost (optional, on the same object).</summary>
        public ManualFire Manual => _manual;

        private float ManualDamage => _manual ? _manual.DamageMultiplier : 1f;
        private float ManualRate => _manual ? _manual.FireRateMultiplier : 1f;

        /// <summary>Damage of one bullet fired by a unit with the given base damage (all multipliers).</summary>
        public double DamageFor(double unitBaseDamage) =>
            (unitBaseDamage + _bonusDamage) * DamageMultiplier * ManualDamage;

        /// <summary>
        /// Damage per second of the whole squad with shop upgrades but without buffs or
        /// the manual boost. Elites size their HP from this.
        /// </summary>
        public double BaseSquadDps
        {
            get
            {
                double volley = baseDamage + _bonusDamage;
                if (_formation)
                {
                    foreach (SquadFormation.Unit unit in _formation.Units)
                        volley += Barracks.PowerOfTier(unit.Tier) + _bonusDamage;
                }
                return volley * (baseFireRate + _bonusFireRate);
            }
        }

        /// <summary>Damage of the player's own bullet.</summary>
        public double PlayerDamage => DamageFor(baseDamage);

        /// <summary>Combined damage of one volley from all living soldiers (excludes the player).</summary>
        public double SoldierDamage
        {
            get
            {
                double total = 0d;
                if (_formation)
                {
                    foreach (SquadFormation.Unit unit in _formation.Units)
                        total += DamageFor(Barracks.PowerOfTier(unit.Tier));
                }
                return total;
            }
        }

        /// <summary>Combined soldier damage for a planned front line (menu preview).</summary>
        public double EstimateSoldierDamage(IEnumerable<int> soldierTiers)
        {
            double total = 0d;
            foreach (int tier in soldierTiers) total += DamageFor(Barracks.PowerOfTier(tier));
            return total;
        }

        public float FireRate => Mathf.Max(0.1f, (baseFireRate + _bonusFireRate) * FireRateMultiplier * ManualRate);
        public float BulletSpeed => bulletSpeed;

        private LaneSystem Lanes => laneSystem ? laneSystem : LaneSystem.Instance;

        private Transform _bulletContainer;
        private SquadFormation _formation;
        private ManualFire _manual;
        private float _cooldown;
        private int _bonusDamage;
        private float _bonusFireRate;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _formation = GetComponent<SquadFormation>();
            _manual = GetComponent<ManualFire>();
        }

        private void OnEnable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted += ResetForRound;
        }

        private void OnDisable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted -= ResetForRound;

            // Back in the menu: show plain stats (a continued run re-applies its effects).
            DamageMultiplier = 1f;
            FireRateMultiplier = 1f;
            Suppressed = false;
        }

        private void Update()
        {
            if (GameManager.Instance && !GameManager.Instance.IsPlaying) return;
            if (Suppressed) return;

            // Never wait longer than the current rate allows (pressing FIRE takes effect at once).
            _cooldown = Mathf.Min(_cooldown - Time.deltaTime, 1f / FireRate);
            if (_cooldown > 0f) return;

            FireVolley();
            _cooldown = 1f / FireRate;
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Override the base tuning values (e.g. when switching starting weapon).</summary>
        public void SetBaseStats(int damage, float fireRate)
        {
            baseDamage = Mathf.Max(1, damage);
            baseFireRate = Mathf.Max(0.1f, fireRate);
        }

        /// <summary>Permanent bonuses bought in the shop (added to the base stats).</summary>
        public void SetUpgradeBonuses(int bonusDamage, float bonusFireRate)
        {
            _bonusDamage = Mathf.Max(0, bonusDamage);
            _bonusFireRate = Mathf.Max(0f, bonusFireRate);
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private void ResetForRound()
        {
            DamageMultiplier = 1f;
            FireRateMultiplier = 1f;
            Suppressed = false;
            _cooldown = 0f;

            if (!_bulletContainer) return;
            for (int i = _bulletContainer.childCount - 1; i >= 0; i--)
                Destroy(_bulletContainer.GetChild(i).gameObject);
        }

        private void FireVolley()
        {
            Vector3 playerPos = transform.position;
            bool manual = _manual && _manual.IsFiring;
            FireBullet(playerPos + Vector3.up * 0.5f, PlayerDamage, manual);

            if (!_formation) return;
            var units = _formation.Units;
            for (int i = 0; i < units.Count; i++)
            {
                // Pull soldier bullets in towards the player's column so the whole squad
                // hits what the player is lined up with.
                Vector3 origin = units[i].Transform.position + Vector3.up * 0.2f;
                origin.x = playerPos.x + Mathf.Clamp(origin.x - playerPos.x, -soldierBulletSpread, soldierBulletSpread);
                FireBullet(origin, DamageFor(Barracks.PowerOfTier(units[i].Tier)), manual);
            }
        }

        private void FireBullet(Vector3 origin, double damage, bool manual)
        {
            var go = new GameObject("Bullet");
            go.transform.SetParent(GetBulletContainer(), false);
            go.transform.position = origin;
            go.transform.localScale = new Vector3(bulletSize.x, bulletSize.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = bulletSprite;
            sr.color = manual ? manualBulletColor : bulletColor;
            sr.sortingOrder = 8;

            float maxY = Lanes ? Lanes.SpawnLineWorldY + 1f : transform.position.y + 20f;
            go.AddComponent<Bullet>().Initialize(damage, bulletSpeed, maxY, manual);
        }

        /// <summary>
        /// Bullets live next to the player (not under it) so they don't move with it,
        /// but still inside the gameplay root so they hide with the rest of the round.
        /// </summary>
        private Transform GetBulletContainer()
        {
            if (_bulletContainer) return _bulletContainer;
            _bulletContainer = new GameObject("Bullets").transform;
            _bulletContainer.SetParent(transform.parent, false);
            return _bulletContainer;
        }
    }
}
