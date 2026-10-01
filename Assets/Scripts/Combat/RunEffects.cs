using System;
using System.Collections.Generic;
using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;

namespace SectorCleanse.Combat
{
    /// <summary>Every buff and debuff. Stored as int in saves: only append new values at the end.</summary>
    public enum EffectType
    {
        // Buffs (dropped by elites killed in time; catch the drop)
        DoubleDamage,
        DoubleFireRate,
        Bounty,
        Shield,
        Reinforcement,
        BossReinforcement,

        // Debuffs (applied when an elite's timer runs out)
        LoweredFireRate,
        Jammed,
        Swarm,
        CompromisedPosition,
        Sabotage,
    }

    /// <summary>Display name, kind and duration of an effect. Duration 0 = instant.</summary>
    public readonly struct EffectInfo
    {
        public readonly string Name;
        public readonly bool IsBuff;
        public readonly float Duration;

        public EffectInfo(string name, bool isBuff, float duration)
        {
            Name = name;
            IsBuff = isBuff;
            Duration = duration;
        }

        public bool IsTimed => Duration > 0f;
        public Color Color => IsBuff ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.4f, 0.35f);

        public static readonly EffectType[] BuffPool =
        {
            EffectType.DoubleDamage, EffectType.DoubleFireRate, EffectType.Bounty,
            EffectType.Shield, EffectType.Reinforcement,
        };

        public static readonly EffectType[] DebuffPool =
        {
            EffectType.LoweredFireRate, EffectType.Jammed, EffectType.Swarm,
            EffectType.CompromisedPosition, EffectType.Sabotage,
        };

        public static EffectInfo Of(EffectType type)
        {
            switch (type)
            {
                case EffectType.DoubleDamage: return new EffectInfo("x2 DAMAGE", true, 10f);
                case EffectType.DoubleFireRate: return new EffectInfo("x2 FIRE RATE", true, 10f);
                case EffectType.Bounty: return new EffectInfo("BOUNTY x2 $", true, 15f);
                case EffectType.Shield: return new EffectInfo("SHIELD", true, 0f);
                case EffectType.Reinforcement: return new EffectInfo("REINFORCEMENT", true, 0f);
                case EffectType.BossReinforcement: return new EffectInfo("ELITE REINFORCEMENT", true, 0f);
                case EffectType.LoweredFireRate: return new EffectInfo("LOWERED FIRE RATE", false, 10f);
                case EffectType.Jammed: return new EffectInfo("JAMMED", false, 3f);
                case EffectType.Swarm: return new EffectInfo("SWARM", false, 8f);
                case EffectType.CompromisedPosition: return new EffectInfo("COMPROMISED POSITION", false, 0f);
                case EffectType.Sabotage: return new EffectInfo("SABOTAGE -25% $", false, 0f);
                default: return new EffectInfo(type.ToString().ToUpperInvariant(), true, 0f);
            }
        }
    }

    /// <summary>
    /// Applies and times buffs/debuffs for the current run.
    ///
    /// Timed effects (re-applying refreshes the timer):
    ///  * x2 DAMAGE / x2 FIRE RATE (10 s), BOUNTY: kills pay x2 (15 s)
    ///  * LOWERED FIRE RATE: x0.5 (10 s), JAMMED: no firing (3 s), SWARM: x2 spawns (8 s)
    /// Instant effects:
    ///  * SHIELD: blocks the next breach; REINFORCEMENT: a soldier of your best tier
    ///    (ELITE REINFORCEMENT from bosses: one tier higher), replacing the weakest if full
    ///  * COMPROMISED POSITION: lose the 2 weakest soldiers; SABOTAGE: lose 25% of round money
    ///
    /// Active timed effects are saved with the run.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunEffects : MonoBehaviour, IRunStatePersistent
    {
        public class ActiveEffect
        {
            public EffectType Type;
            public float Remaining;
            public float Duration;
        }

        [Header("References (optional, found automatically)")]
        [SerializeField] private Weapon weapon;
        [SerializeField] private PlayerSquad squad;
        [SerializeField] private EnemySpawner spawner;

        [SerializeField, Min(1)] private int compromisedSoldiers = 2;
        [SerializeField, Range(0f, 1f)] private float sabotageFraction = 0.25f;

        private readonly List<ActiveEffect> _active = new List<ActiveEffect>();

        /// <summary>Timed effects currently running.</summary>
        public IReadOnlyList<ActiveEffect> Active => _active;

        /// <summary>Raised after any effect (timed or instant) is applied.</summary>
        public event Action<EffectType> Applied;

        private void Awake()
        {
            if (!weapon) weapon = FindAnyObjectByType<Weapon>();
            if (!squad) squad = FindAnyObjectByType<PlayerSquad>();
            if (!spawner) spawner = FindAnyObjectByType<EnemySpawner>();
        }

        private void OnEnable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted += ResetForRound;
            RunSave.Register(this);
        }

        private void OnDisable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted -= ResetForRound;
            RunSave.Unregister(this);
        }

        private void Update()
        {
            if (GameManager.Instance && !GameManager.Instance.IsPlaying) return;
            if (_active.Count == 0) return;

            float dt = Time.deltaTime;
            bool expired = false;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].Remaining -= dt;
                if (_active[i].Remaining > 0f) continue;
                _active.RemoveAt(i);
                expired = true;
            }
            if (expired) ApplyModifiers();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        public bool Has(EffectType type) => _active.Exists(e => e.Type == type);

        /// <summary>Apply an effect. <paramref name="durationScale"/> stretches timed effects (perfect elite kills).</summary>
        public void Apply(EffectType type, float durationScale = 1f)
        {
            EffectInfo info = EffectInfo.Of(type);
            if (info.IsTimed) AddTimed(type, info.Duration * Mathf.Max(0.1f, durationScale));
            else ApplyInstant(type);

            ApplyModifiers();
            Applied?.Invoke(type);
        }

        public void SaveRunState(RunSaveData data)
        {
            data.effects.Clear();
            foreach (ActiveEffect e in _active)
                data.effects.Add(new SavedEffect((int)e.Type, e.Remaining, e.Duration));
        }

        public void LoadRunState(RunSaveData data)
        {
            _active.Clear();
            if (data.effects != null)
            {
                foreach (SavedEffect saved in data.effects)
                {
                    if (saved.remaining <= 0f || !Enum.IsDefined(typeof(EffectType), saved.type)) continue;
                    var type = (EffectType)saved.type;
                    if (!EffectInfo.Of(type).IsTimed) continue;
                    _active.Add(new ActiveEffect
                    {
                        Type = type,
                        Remaining = saved.remaining,
                        Duration = Mathf.Max(saved.duration, saved.remaining),
                    });
                }
            }
            ApplyModifiers();
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private void ResetForRound()
        {
            _active.Clear();
            ApplyModifiers();
        }

        private void AddTimed(EffectType type, float duration)
        {
            ActiveEffect existing = _active.Find(e => e.Type == type);
            if (existing != null)
            {
                existing.Remaining = Mathf.Max(existing.Remaining, duration);
                existing.Duration = Mathf.Max(existing.Duration, existing.Remaining);
                return;
            }
            _active.Add(new ActiveEffect { Type = type, Remaining = duration, Duration = duration });
        }

        private void ApplyInstant(EffectType type)
        {
            switch (type)
            {
                case EffectType.Shield:
                    if (squad) squad.AddShield();
                    break;
                case EffectType.Reinforcement:
                    AddReinforcement(0);
                    break;
                case EffectType.BossReinforcement:
                    AddReinforcement(1);
                    break;
                case EffectType.CompromisedPosition:
                    if (squad) squad.RemoveSoldiers(compromisedSoldiers);
                    break;
                case EffectType.Sabotage:
                    GameManager gm = GameManager.Instance;
                    if (gm) gm.RemoveRoundMoney(Mathf.CeilToInt(gm.RoundMoney * sabotageFraction));
                    break;
            }
        }

        /// <summary>A soldier of the squad's best tier (+ bonus) for this run; replaces the weakest if full.</summary>
        private void AddReinforcement(int tierBonus)
        {
            if (!squad) return;
            int tier = Mathf.Clamp(Mathf.Max(0, squad.BestTier) + tierBonus, 0, Barracks.MaxTier);
            if (squad.Soldiers.Count >= Barracks.FrontLineCap) squad.RemoveSoldiers(1);
            squad.AddSoldier(tier);
        }

        /// <summary>Push the combined effect multipliers into the systems they affect.</summary>
        private void ApplyModifiers()
        {
            if (weapon)
            {
                weapon.DamageMultiplier = Has(EffectType.DoubleDamage) ? 2f : 1f;
                weapon.FireRateMultiplier =
                    (Has(EffectType.DoubleFireRate) ? 2f : 1f) * (Has(EffectType.LoweredFireRate) ? 0.5f : 1f);
                weapon.Suppressed = Has(EffectType.Jammed);
            }
            if (spawner) spawner.EffectRateMultiplier = Has(EffectType.Swarm) ? 2f : 1f;
            if (GameManager.Instance) GameManager.Instance.MoneyMultiplier = Has(EffectType.Bounty) ? 2f : 1f;
        }
    }
}
