using System.Collections.Generic;
using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;

namespace SectorCleanse.Combat
{
    /// <summary>
    /// A graybox enemy: moves straight down its lane and shows its remaining HP.
    ///
    /// * Killed by bullets → pays $1 per wave number ($1 on wave 1, $2 on wave 2…),
    ///   doubled for kills made while holding FIRE (see <see cref="ManualFire"/>).
    /// * Reaches the player line → if the player is standing in this lane, the squad
    ///   loses soldiers equal to the enemy's remaining HP. Either way it despawns.
    ///
    /// All live enemies are tracked in <see cref="Active"/> so bullets can do simple
    /// overlap checks without needing physics. <see cref="Elite"/> derives from this.
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

        protected float Speed;
        protected LaneSystem Lanes;
        protected PlayerController TrackedPlayer;
        protected PlayerSquad TrackedSquad;
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
            Speed = speed;
            Lanes = lanes;
            TrackedPlayer = player;
            TrackedSquad = squad;

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
            if (IsDead || !Lanes) return;
            if (GameManager.Instance && !GameManager.Instance.IsPlaying) return; // Freeze on game over.
            Tick(Time.deltaTime);
        }

        /// <summary>Per-frame behaviour while alive and the round is running.</summary>
        protected virtual void Tick(float dt)
        {
            transform.position += Vector3.down * (Speed * dt);
            if (transform.position.y <= Lanes.PlayerLineWorldY) ReachPlayerLine();
        }

        /// <summary>A bullet hit this enemy. <paramref name="manual"/> = fired while holding FIRE.</summary>
        public virtual void TakeHit(double amount, bool manual) => TakeDamage(amount, manual);

        /// <summary>Apply damage. Kills (and pays out) at 0 HP.</summary>
        public void TakeDamage(double amount, bool manual = false)
        {
            if (IsDead || amount <= 0) return;

            Hp = System.Math.Max(0d, Hp - amount);
            RefreshLabel();

            if (Hp <= 0)
            {
                OnKilled(manual);
                Destroy(gameObject);
            }
        }

        /// <summary>Pay out. Manual kills pay <see cref="ManualFire.MoneyBoost"/>×.</summary>
        protected virtual void OnKilled(bool manual)
        {
            if (GameManager.Instance) GameManager.Instance.AddKillReward(manual ? ManualFire.MoneyBoost : 1f);
        }

        /// <summary>Remove without reward or damage (round reset).</summary>
        public void Despawn()
        {
            _despawned = true;
            Destroy(gameObject);
        }

        protected virtual void ReachPlayerLine()
        {
            // Only hurts the squad if the player is standing in this lane.
            if (TrackedPlayer && TrackedSquad && TrackedPlayer.CurrentLane == Lane) TrackedSquad.TakeDamage(Hp);
            Despawn();
        }

        // ------------------------------------------------------------------
        // HP label (graybox: built-in TextMesh, no assets needed)
        // ------------------------------------------------------------------

        protected void CreateLabel()
        {
            if (_label) return;
            _label = CreateWorldText("HP", Vector3.zero, 0.08f, Color.white);
        }

        protected virtual void RefreshLabel()
        {
            if (_label) _label.text = NumberFormat.Short(System.Math.Ceiling(Hp));
        }

        /// <summary>A world-space text label child (graybox UI on enemies).</summary>
        protected TextMesh CreateWorldText(string name, Vector3 localPosition, float characterSize, Color color)
        {
            if (!_labelFont) _labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;

            var text = go.AddComponent<TextMesh>();
            text.font = _labelFont;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;

            var meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _labelFont.material;
            meshRenderer.sortingOrder = 20;
            return text;
        }
    }
}
