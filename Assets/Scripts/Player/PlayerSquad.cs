using System;
using System.Collections.Generic;
using SectorCleanse.Core;
using UnityEngine;

namespace SectorCleanse.Player
{
    /// <summary>
    /// The squad fighting this run: the player (commander) plus up to
    /// <see cref="Barracks.FrontLineCap"/> deployed soldiers.
    ///
    /// Health model:
    ///  * Each soldier has HP = its damage = 5^tier.
    ///  * Breach damage hits the weakest soldier first and overflows to the next;
    ///    the player is hit only once every soldier is down.
    ///  * Soldiers lost during a run are only gone for that run; the barracks keeps them.
    ///  * The round ends when the player falls.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSquad : MonoBehaviour, IRunStatePersistent
    {
        public class Soldier
        {
            public int Tier;
            public double MaxHp;
            public double Hp;
        }

        [Tooltip("Optional. Found automatically if left empty.")]
        [SerializeField] private Barracks barracks;

        [Tooltip("The player's own HP (protected by the soldiers).")]
        [SerializeField, Min(1f)] private float playerMaxHp = 1f;

        private readonly List<Soldier> _soldiers = new List<Soldier>();

        /// <summary>Living soldiers (excludes the player), strongest first.</summary>
        public IReadOnlyList<Soldier> Soldiers => _soldiers;

        public double PlayerHp { get; private set; }
        public double PlayerMaxHp => playerMaxHp;
        public bool IsAlive => PlayerHp > 0;

        /// <summary>Living fighters including the player.</summary>
        public int SoldierCount => _soldiers.Count + (IsAlive ? 1 : 0);

        public double TotalHp
        {
            get
            {
                double total = Math.Max(0, PlayerHp);
                foreach (Soldier s in _soldiers) total += s.Hp;
                return total;
            }
        }

        /// <summary>Raised whenever soldiers are added, damaged or lost.</summary>
        public event Action SquadChanged;

        /// <summary>Raised once when the player falls, just before the round ends.</summary>
        public event Action SquadWiped;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (!barracks) barracks = FindAnyObjectByType<Barracks>();
        }

        private void OnEnable()
        {
            // See PlayerController.OnEnable for why this is OnEnable and not Start.
            if (GameManager.Instance) GameManager.Instance.RoundStarted += ResetSquad;
            RunSave.Register(this);
        }

        private void OnDisable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted -= ResetSquad;
            RunSave.Unregister(this);
        }

        private void Start()
        {
            // Covers isolated testing without a GameManager; harmless otherwise.
            if (!IsAlive) ResetSquad();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Breach damage: weakest soldier first, overflow continues, player last.</summary>
        public void TakeDamage(double amount)
        {
            if (amount <= 0 || !IsAlive) return;

            while (amount > 0 && _soldiers.Count > 0)
            {
                Soldier weakest = _soldiers[_soldiers.Count - 1]; // List is kept strongest-first.
                double dealt = Math.Min(amount, weakest.Hp);
                weakest.Hp -= dealt;
                amount -= dealt;
                if (weakest.Hp <= 0) _soldiers.RemoveAt(_soldiers.Count - 1);
                else SortStrongestFirst();
            }

            if (amount > 0) PlayerHp = Math.Max(0, PlayerHp - amount);

            SquadChanged?.Invoke();
            if (!IsAlive) HandleWiped();
        }

        /// <summary>Add a soldier mid-run (e.g. a recruit buff). Ignored when the front line is full.</summary>
        public void AddSoldier(int tier)
        {
            if (!IsAlive || _soldiers.Count >= Barracks.FrontLineCap) return;
            double hp = Barracks.PowerOfTier(tier);
            _soldiers.Add(new Soldier { Tier = tier, MaxHp = hp, Hp = hp });
            SortStrongestFirst();
            SquadChanged?.Invoke();
        }

        /// <summary>Remove soldiers outright, weakest first (e.g. "Compromised Position").</summary>
        public void RemoveSoldiers(int count)
        {
            if (count <= 0 || _soldiers.Count == 0) return;
            int removed = Mathf.Min(count, _soldiers.Count);
            _soldiers.RemoveRange(_soldiers.Count - removed, removed);
            SquadChanged?.Invoke();
        }

        // ------------------------------------------------------------------
        // Run state
        // ------------------------------------------------------------------

        public void SaveRunState(RunSaveData data)
        {
            data.playerHp = PlayerHp;
            data.soldiers.Clear();
            foreach (Soldier s in _soldiers) data.soldiers.Add(new SavedSoldier(s.Tier, s.Hp));
        }

        public void LoadRunState(RunSaveData data)
        {
            _soldiers.Clear();
            foreach (SavedSoldier saved in data.soldiers)
            {
                if (saved.hp <= 0) continue;
                _soldiers.Add(new Soldier
                {
                    Tier = saved.tier,
                    MaxHp = Barracks.PowerOfTier(saved.tier),
                    Hp = saved.hp,
                });
            }
            SortStrongestFirst();
            PlayerHp = data.playerHp > 0 ? data.playerHp : playerMaxHp;
            SquadChanged?.Invoke();
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        /// <summary>Fresh run: full-health copies of the barracks front line.</summary>
        private void ResetSquad()
        {
            _soldiers.Clear();
            if (barracks)
            {
                foreach (int tier in barracks.GetDeployedTiers())
                {
                    double hp = Barracks.PowerOfTier(tier);
                    _soldiers.Add(new Soldier { Tier = tier, MaxHp = hp, Hp = hp });
                }
            }
            SortStrongestFirst();
            PlayerHp = playerMaxHp;
            SquadChanged?.Invoke();
        }

        private void SortStrongestFirst() =>
            _soldiers.Sort((a, b) => b.Hp.CompareTo(a.Hp));

        private void HandleWiped()
        {
            SquadWiped?.Invoke();
            if (GameManager.Instance) GameManager.Instance.EndRound();
        }

#if UNITY_EDITOR
        // Right-click the component header in Play Mode to test without enemies.
        [ContextMenu("Debug/Take 1 Damage")]
        private void DebugTakeDamage() => TakeDamage(1);

        [ContextMenu("Debug/Add T0 Soldier")]
        private void DebugAddSoldier() => AddSoldier(0);
#endif
    }
}
