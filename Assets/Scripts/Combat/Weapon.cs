using SectorCleanse.Core;
using UnityEngine;

namespace SectorCleanse.Combat
{
    /// <summary>
    /// Auto-fires bullets straight up from its transform while a round is running.
    ///
    /// Stats are split into a base value (set by meta-upgrades via
    /// <see cref="SetBaseStats"/>) and per-round multipliers (for buffs/debuffs such
    /// as "Lowered Fire Rate"). Multipliers reset at the start of each round.
    /// </summary>
    [DisallowMultipleComponent]
    public class Weapon : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional. Falls back to LaneSystem.Instance. Used to know when bullets leave the play area.")]
        [SerializeField] private LaneSystem laneSystem;

        [Header("Base stats (permanent upgrades raise these)")]
        [SerializeField, Min(1)] private int baseDamage = 1;

        [Tooltip("Shots per second.")]
        [SerializeField, Min(0.1f)] private float baseFireRate = 4f;

        [Header("Bullets")]
        [SerializeField, Min(0.1f)] private float bulletSpeed = 14f;
        [SerializeField] private Vector2 bulletSize = new Vector2(0.15f, 0.4f);
        [SerializeField] private Sprite bulletSprite;
        [SerializeField] private Color bulletColor = new Color(1f, 0.9f, 0.3f);

        /// <summary>Temporary damage modifier (buffs). Reset to 1 each round.</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>Temporary fire-rate modifier (buffs / "Lowered Fire Rate"). Reset to 1 each round.</summary>
        public float FireRateMultiplier { get; set; } = 1f;

        public int Damage => Mathf.Max(1, Mathf.RoundToInt(baseDamage * DamageMultiplier));
        public float FireRate => Mathf.Max(0.1f, baseFireRate * FireRateMultiplier);

        private LaneSystem Lanes => laneSystem ? laneSystem : LaneSystem.Instance;

        private Transform _bulletContainer;
        private float _cooldown;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void OnEnable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted += ResetForRound;
        }

        private void OnDisable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted -= ResetForRound;
        }

        private void Update()
        {
            if (GameManager.Instance && !GameManager.Instance.IsPlaying) return;

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;

            Fire();
            _cooldown = 1f / FireRate;
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Set permanent stats (called by the meta-progression system before a round).</summary>
        public void SetBaseStats(int damage, float fireRate)
        {
            baseDamage = Mathf.Max(1, damage);
            baseFireRate = Mathf.Max(0.1f, fireRate);
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private void ResetForRound()
        {
            DamageMultiplier = 1f;
            FireRateMultiplier = 1f;
            _cooldown = 0f;

            if (!_bulletContainer) return;
            for (int i = _bulletContainer.childCount - 1; i >= 0; i--)
                Destroy(_bulletContainer.GetChild(i).gameObject);
        }

        private void Fire()
        {
            var go = new GameObject("Bullet");
            go.transform.SetParent(GetBulletContainer(), false);
            go.transform.position = transform.position + Vector3.up * 0.5f;
            go.transform.localScale = new Vector3(bulletSize.x, bulletSize.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = bulletSprite;
            sr.color = bulletColor;
            sr.sortingOrder = 8;

            float maxY = Lanes ? Lanes.SpawnLineWorldY + 1f : transform.position.y + 20f;
            go.AddComponent<Bullet>().Initialize(Damage, bulletSpeed, maxY);
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
