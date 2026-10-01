using System;
using SectorCleanse.Core;
using UnityEngine;

namespace SectorCleanse.Player
{
    /// <summary>
    /// The squad's soldier count, which doubles as its health pool.
    ///
    /// The player character counts as one soldier, so a squad of 1 is just the
    /// player. Enemies reaching the player line deal damage equal to their remaining
    /// HP, which removes that many soldiers. At 0 the round ends.
    ///
    /// Later systems plug in here:
    ///  * Recruit buffs        -> <see cref="AddSoldiers"/>
    ///  * Compromised Position -> <see cref="RemoveSoldiers"/>
    ///  * Enemy breach         -> <see cref="TakeDamage"/>
    ///  * Meta-progression     -> <see cref="SetStartingSoldiers"/> before a round starts
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSquad : MonoBehaviour
    {
        [Tooltip("Base soldiers at round start, including the player. Shop upgrades add to this.")]
        [SerializeField, Min(1)] private int startingSoldiers = 1;

        [Tooltip("Hard cap on squad size (keeps the graybox readable; 0 = unlimited).")]
        [SerializeField, Min(0)] private int maxSoldiers;

        public int SoldierCount { get; private set; }

        /// <summary>Soldiers at round start: base + permanently bought soldiers.</summary>
        public int StartingSoldiers => startingSoldiers + _bonusSoldiers;

        private int _bonusSoldiers;
        public bool IsAlive => SoldierCount > 0;

        /// <summary>Raised with the new count whenever it changes.</summary>
        public event Action<int> SoldierCountChanged;

        /// <summary>Raised once when the count hits 0, just before the round ends.</summary>
        public event Action SquadWiped;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void OnEnable()
        {
            // See PlayerController.OnEnable for why this is OnEnable and not Start.
            if (GameManager.Instance) GameManager.Instance.RoundStarted += ResetSquad;
        }

        private void OnDisable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted -= ResetSquad;
        }

        private void Start()
        {
            // Covers isolated testing without a GameManager; harmless otherwise.
            if (SoldierCount == 0) ResetSquad();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Set by the upgrade/meta system before a round starts.</summary>
        public void SetStartingSoldiers(int count) => startingSoldiers = Mathf.Max(1, count);

        /// <summary>Permanently bought soldiers (set by the upgrade shop). Applies from the next round.</summary>
        public void SetBonusSoldiers(int count) => _bonusSoldiers = Mathf.Max(0, count);

        public void AddSoldiers(int amount)
        {
            if (amount <= 0 || !IsAlive) return;

            int newCount = SoldierCount + amount;
            if (maxSoldiers > 0) newCount = Mathf.Min(newCount, maxSoldiers);
            SetCount(newCount);
        }

        public void RemoveSoldiers(int amount)
        {
            if (amount <= 0 || !IsAlive) return;

            SetCount(Mathf.Max(0, SoldierCount - amount));
            if (!IsAlive) HandleWiped();
        }

        /// <summary>Damage from an enemy that reached the player line (amount = its remaining HP).</summary>
        public void TakeDamage(int amount) => RemoveSoldiers(amount);

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private void ResetSquad()
        {
            int count = StartingSoldiers;
            if (maxSoldiers > 0) count = Mathf.Min(count, maxSoldiers);
            SetCount(count);
        }

        private void SetCount(int value)
        {
            if (value == SoldierCount) return;
            SoldierCount = value;
            SoldierCountChanged?.Invoke(SoldierCount);
        }

        private void HandleWiped()
        {
            SquadWiped?.Invoke();
            if (GameManager.Instance) GameManager.Instance.EndRound();
        }

#if UNITY_EDITOR
        // Right-click the component header in Play Mode to test the death loop without enemies.
        [ContextMenu("Debug/Take 1 Damage")]
        private void DebugTakeDamage() => TakeDamage(1);

        [ContextMenu("Debug/Add 1 Soldier")]
        private void DebugAddSoldier() => AddSoldiers(1);
#endif
    }
}
