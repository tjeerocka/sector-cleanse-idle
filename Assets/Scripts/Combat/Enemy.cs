using System.Collections.Generic;
using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;

namespace SectorCleanse.Combat
{
    /// <summary>
    /// A graybox enemy: moves straight down its lane and shows its remaining HP.
    ///
    /// * Killed by bullets → pays money equal to its starting HP.
    /// * Reaches the player line → if the player is standing in this lane, the squad
    ///   loses soldiers equal to the enemy's remaining HP. Either way it despawns.
    ///
    /// All live enemies are tracked in <see cref="Active"/> so bullets can do simple
    /// overlap checks without needing physics.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        private static readonly List<Enemy> ActiveEnemies = new List<Enemy>();
        private static Font _labelFont;

        /// <summary>All enemies currently in play.</summary>
        public static IReadOnlyList<Enemy> Active => ActiveEnemies;

        public int Lane { get; private set; }
        public double MaxHp { get; private set; }
        public double Hp { get; private set; }

        /// <summary>True once killed or despawned; such enemies ignore further hits.</summary>
        public bool IsDead => Hp <= 0 || _despawned;

        public float HalfWidth => 0.5f * transform.lossyScale.x;
        public float HalfHeight => 0.5f * transform.lossyScale.y;

        private float _speed;
        private LaneSystem _lanes;
        private PlayerController _player;
        private PlayerSquad _squad;
        private TextMesh _label;
        private bool _despawned;

        // ------------------------------------------------------------------
        // Setup
        // ------------------------------------------------------------------

        public void Initialize(int lane, double hp, float speed, LaneSystem lanes,
            PlayerController player, PlayerSquad squad)
        {
            Lane = lane;
            MaxHp = System.Math.Max(1d, hp);
            Hp = MaxHp;
            _speed = speed;
            _lanes = lanes;
            _player = player;
            _squad = squad;

            CreateLabel();
            RefreshLabel();
        }

        private void OnEnable() => ActiveEnemies.Add(this);

        private void OnDisable() => ActiveEnemies.Remove(this);

        // ------------------------------------------------------------------
        // Behaviour
        // ------------------------------------------------------------------

        private void Update()
        {
            if (IsDead || !_lanes) return;
            if (GameManager.Instance && !GameManager.Instance.IsPlaying) return; // Freeze on game over.

            transform.position += Vector3.down * (_speed * Time.deltaTime);

            if (transform.position.y <= _lanes.PlayerLineWorldY) ReachPlayerLine();
        }

        /// <summary>Apply weapon damage. Kills (and pays out) at 0 HP.</summary>
        public void TakeDamage(double amount)
        {
            if (IsDead || amount <= 0) return;

            Hp = System.Math.Max(0d, Hp - amount);
            RefreshLabel();

            if (Hp <= 0)
            {
                // Reward = starting HP (clamped so huge HP can't overflow the int bank).
                int reward = (int)System.Math.Min(MaxHp, int.MaxValue / 4);
                if (GameManager.Instance) GameManager.Instance.AddRoundMoney(reward);
                Destroy(gameObject);
            }
        }

        /// <summary>Remove without reward or damage (round reset).</summary>
        public void Despawn()
        {
            _despawned = true;
            Destroy(gameObject);
        }

        private void ReachPlayerLine()
        {
            // Only hurts the squad if the player is standing in this lane.
            if (_player && _squad && _player.CurrentLane == Lane) _squad.TakeDamage(Hp);
            Despawn();
        }

        // ------------------------------------------------------------------
        // HP label (graybox: built-in TextMesh, no assets needed)
        // ------------------------------------------------------------------

        private void CreateLabel()
        {
            if (_label) return;
            if (!_labelFont) _labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("HP");
            go.transform.SetParent(transform, false);

            _label = go.AddComponent<TextMesh>();
            _label.font = _labelFont;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontSize = 64;
            _label.characterSize = 0.08f;
            _label.fontStyle = FontStyle.Bold;
            _label.color = Color.white;

            var meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _labelFont.material;
            meshRenderer.sortingOrder = 20;
        }

        private void RefreshLabel()
        {
            if (_label) _label.text = NumberFormat.Short(System.Math.Ceiling(Hp));
        }
    }
}
