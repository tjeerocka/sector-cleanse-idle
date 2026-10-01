using System;
using System.Collections.Generic;
using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;

namespace SectorCleanse.Combat
{
    /// <summary>
    /// Runs each wave:
    ///
    ///   Wave (waveDuration s of normal enemies) → Elite incoming (banner) → Elite
    ///
    ///  * The elite hovers in a random lane with a countdown and only takes damage from
    ///    manual fire (hold FIRE). Its HP is a few seconds of the squad's boosted
    ///    firepower, never less than a wave-based minimum.
    ///  * Killed in time → a buff drops down its lane (catch it), the wave advances.
    ///    Killed with more than half the time left = PERFECT (longer buff).
    ///  * Timer runs out → a debuff hits and the elite marches down its lane; reaching
    ///    you is devastating. Killing it on the way still advances the wave; if it
    ///    breaks through, the same wave repeats.
    ///  * Checkpoint waves (5, 10, 15…) get a BOSS: more HP, more time, a guaranteed
    ///    Elite Reinforcement drop.
    ///
    /// Normal spawns slow down while an elite is up. Progress through the wave is
    /// saved; an elite on screen is not (it comes back a few seconds after continuing).
    /// </summary>
    [DisallowMultipleComponent]
    public class WaveDirector : MonoBehaviour, IRunStatePersistent
    {
        public enum Phase { Wave, EliteIncoming, Elite }

        [Header("References (optional, found automatically)")]
        [SerializeField] private LaneSystem laneSystem;
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerSquad squad;
        [SerializeField] private Weapon weapon;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private RunEffects effects;
        [SerializeField] private Sprite sprite;

        [Header("Wave")]
        [Tooltip("Seconds of normal enemies before the elite.")]
        [SerializeField, Min(1f)] private float waveDuration = 30f;

        [Tooltip("Seconds between the ELITE INCOMING banner and the elite appearing.")]
        [SerializeField, Min(0f)] private float incomingDelay = 2.5f;

        [Tooltip("Normal spawn rate while an elite is up (0.35 = about a third).")]
        [SerializeField, Range(0f, 1f)] private float eliteSpawnRate = 0.35f;

        [Tooltip("After continuing a saved run during an elite, it returns this many seconds later.")]
        [SerializeField, Min(0f)] private float resumeLeadIn = 3f;

        [Header("Elite")]
        [SerializeField, Min(1f)] private float eliteTime = 15f;
        [Tooltip("Elite HP = this many seconds of the squad's boosted (manual) firepower.")]
        [SerializeField, Min(0.5f)] private float eliteDpsSeconds = 6f;
        [Tooltip("Elite HP is at least this × a normal enemy's HP on the wave.")]
        [SerializeField, Min(1f)] private float eliteMinHpFactor = 15f;
        [Tooltip("Kill pays this × the wave's kill reward.")]
        [SerializeField, Min(0f)] private float eliteReward = 10f;
        [SerializeField, Min(0.1f)] private float eliteSize = 1.4f;
        [SerializeField] private Color eliteColor = new Color(0.7f, 0.3f, 0.95f);

        [Header("Boss (checkpoint waves)")]
        [SerializeField, Min(1f)] private float bossTime = 25f;
        [SerializeField, Min(0.5f)] private float bossDpsSeconds = 10f;
        [SerializeField, Min(1f)] private float bossMinHpFactor = 40f;
        [SerializeField, Min(0f)] private float bossReward = 25f;
        [SerializeField, Min(0.1f)] private float bossSize = 1.75f;
        [SerializeField] private Color bossColor = new Color(0.9f, 0.15f, 0.55f);

        [Header("Movement")]
        [Tooltip("Where the elite hovers: fraction of the way from the spawn line to the player line.")]
        [SerializeField, Range(0f, 0.9f)] private float hoverFraction = 0.3f;
        [SerializeField, Min(0.1f)] private float enterSpeed = 4f;
        [SerializeField, Min(0.1f)] private float marchSpeed = 2.5f;

        [Header("Rewards")]
        [Tooltip("Killing the elite with at least this fraction of its time left is PERFECT.")]
        [SerializeField, Range(0f, 1f)] private float perfectThreshold = 0.5f;
        [Tooltip("PERFECT kills stretch the dropped buff's duration by this factor.")]
        [SerializeField, Min(1f)] private float perfectDurationScale = 1.5f;
        [SerializeField, Min(0.1f)] private float pickupSpeed = 3f;
        [SerializeField, Min(0.1f)] private float pickupSize = 0.6f;
        [SerializeField] private Color pickupColor = new Color(0.35f, 1f, 0.45f);

        public Phase CurrentPhase { get; private set; } = Phase.Wave;

        /// <summary>Seconds into the current wave's normal phase.</summary>
        public float WaveTime { get; private set; }

        public float WaveDuration => waveDuration;

        /// <summary>Seconds until the elite (0 once it is incoming or up).</summary>
        public float TimeUntilElite => CurrentPhase == Phase.Wave ? Mathf.Max(0f, waveDuration - WaveTime) : 0f;

        /// <summary>The elite on screen, or null.</summary>
        public Elite CurrentElite { get; private set; }

        /// <summary>Banner messages for the HUD: (text, color).</summary>
        public event Action<string, Color> Announced;

        private LaneSystem Lanes => laneSystem ? laneSystem : LaneSystem.Instance;

        private Transform _container;
        private float _incomingTimer;
        private bool _incomingIsBoss;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _container = new GameObject("Elites").transform;
            _container.SetParent(transform, false);

            if (!player) player = FindAnyObjectByType<PlayerController>();
            if (!squad) squad = FindAnyObjectByType<PlayerSquad>();
            if (!weapon) weapon = FindAnyObjectByType<Weapon>();
            if (!spawner) spawner = FindAnyObjectByType<EnemySpawner>();
            if (!effects) effects = GetComponent<RunEffects>();
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
            GameManager gm = GameManager.Instance;
            if (!gm || !gm.IsPlaying || !Lanes) return;

            switch (CurrentPhase)
            {
                case Phase.Wave:
                    WaveTime += Time.deltaTime;
                    if (WaveTime >= waveDuration) BeginEliteIncoming(gm.Wave);
                    break;

                case Phase.EliteIncoming:
                    _incomingTimer -= Time.deltaTime;
                    if (_incomingTimer <= 0f) SpawnElite(gm.Wave);
                    break;

                case Phase.Elite:
                    // Safety net: the elite vanished without resolving (should not happen).
                    if (!CurrentElite) StartWavePhase();
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Checkpoint waves (5, 10, 15…) have a boss instead of an elite.</summary>
        public bool IsBossWave(int wave)
        {
            int every = GameManager.Instance ? GameManager.Instance.CheckpointEvery : 5;
            return wave % every == 0;
        }

        /// <summary>Show a banner message on the HUD.</summary>
        public void Announce(string text, Color color) => Announced?.Invoke(text, color);

        /// <summary>A buff drop reached the player line (called by <see cref="BuffPickup"/>).</summary>
        public void CollectPickup(EffectType type, float durationScale, bool caught)
        {
            EffectInfo info = EffectInfo.Of(type);
            if (!caught)
            {
                Announce($"MISSED {info.Name}", new Color(0.75f, 0.75f, 0.8f));
                return;
            }

            if (effects) effects.Apply(type, durationScale);
            string duration = info.IsTimed ? $" ({Mathf.RoundToInt(info.Duration * durationScale)}s)" : "";
            Announce($"+ {info.Name}{duration}", info.Color);
        }

        public void SaveRunState(RunSaveData data)
        {
            // An elite on screen isn't saved: it returns shortly after continuing.
            data.waveTime = CurrentPhase == Phase.Wave
                ? WaveTime
                : Mathf.Max(0f, waveDuration - resumeLeadIn);
        }

        public void LoadRunState(RunSaveData data)
        {
            StartWavePhase();
            WaveTime = Mathf.Clamp(data.waveTime, 0f, waveDuration);
        }

        // ------------------------------------------------------------------
        // Phases
        // ------------------------------------------------------------------

        private void ResetForRound()
        {
            ClearElite();
            if (_container)
            {
                for (int i = _container.childCount - 1; i >= 0; i--)
                    Destroy(_container.GetChild(i).gameObject);
            }
            StartWavePhase();

            int wave = GameManager.Instance ? GameManager.Instance.Wave : 1;
            Announce($"WAVE {wave}", Color.white);
        }

        private void StartWavePhase()
        {
            CurrentPhase = Phase.Wave;
            WaveTime = 0f;
            if (spawner) spawner.PhaseRateMultiplier = 1f;
        }

        private void BeginEliteIncoming(int wave)
        {
            CurrentPhase = Phase.EliteIncoming;
            _incomingTimer = incomingDelay;
            _incomingIsBoss = IsBossWave(wave);
            if (spawner) spawner.PhaseRateMultiplier = eliteSpawnRate;

            Announce(_incomingIsBoss ? "BOSS INCOMING!\nHOLD FIRE TO HURT IT" : "ELITE INCOMING!\nHOLD FIRE TO HURT IT",
                new Color(1f, 0.6f, 0.2f));
        }

        private void SpawnElite(int wave)
        {
            bool boss = _incomingIsBoss;
            int lane = UnityEngine.Random.Range(0, Lanes.LaneCount);

            // HP: seconds of the squad's boosted firepower, with a wave-based floor.
            double boostedDps = weapon
                ? weapon.BaseSquadDps * ManualFire.DamageBoost * ManualFire.FireRateBoost
                : 16d;
            double minHp = (spawner ? spawner.AverageHpForWave(wave) : 3f + wave * 3f) *
                           (boss ? bossMinHpFactor : eliteMinHpFactor);
            double hp = Math.Ceiling(Math.Max(boostedDps * (boss ? bossDpsSeconds : eliteDpsSeconds), minHp));

            float size = boss ? bossSize : eliteSize;
            var go = new GameObject($"{(boss ? "Boss" : "Elite")}_W{wave}_L{lane}");
            go.transform.SetParent(_container, false);
            go.transform.position = Lanes.GetSpawnPosition(lane) + Vector3.up * size * 0.5f;
            go.transform.localScale = new Vector3(size, size, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = boss ? bossColor : eliteColor;
            sr.sortingOrder = 6;

            float hoverY = Mathf.Lerp(Lanes.SpawnLineWorldY, Lanes.PlayerLineWorldY, hoverFraction);
            var elite = go.AddComponent<Elite>();
            elite.Setup(lane, hp, Lanes, player, squad, boss, boss ? bossTime : eliteTime, hoverY,
                enterSpeed, marchSpeed, boss ? bossReward : eliteReward);
            elite.TimerExpired += OnEliteTimerExpired;
            elite.Resolved += OnEliteResolved;

            CurrentElite = elite;
            CurrentPhase = Phase.Elite;
        }

        private void OnEliteTimerExpired(Elite elite)
        {
            EffectType debuff = EffectInfo.DebuffPool[UnityEngine.Random.Range(0, EffectInfo.DebuffPool.Length)];
            if (effects) effects.Apply(debuff);
            Announce($"TOO SLOW: {EffectInfo.Of(debuff).Name}\nDODGE THE {(elite.IsBoss ? "BOSS" : "ELITE")}!",
                EffectInfo.Of(debuff).Color);
        }

        private void OnEliteResolved(Elite elite, bool killed)
        {
            elite.TimerExpired -= OnEliteTimerExpired;
            elite.Resolved -= OnEliteResolved;
            if (CurrentElite == elite) CurrentElite = null;

            GameManager gm = GameManager.Instance;
            if (!gm || !gm.IsPlaying) return; // The breach ended the run.

            // Reset the phase before advancing: AdvanceWave saves the run.
            StartWavePhase();

            if (!killed)
            {
                Announce($"IT BROKE THROUGH\nWAVE {gm.Wave} AGAIN", new Color(1f, 0.4f, 0.35f));
                return;
            }

            bool inTime = elite.State != Elite.Phase.Marching;
            if (inTime)
            {
                bool perfect = elite.TimeLeftFraction >= perfectThreshold;
                EffectType drop = elite.IsBoss
                    ? EffectType.BossReinforcement
                    : EffectInfo.BuffPool[UnityEngine.Random.Range(0, EffectInfo.BuffPool.Length)];
                SpawnPickup(elite.Lane, elite.transform.position, drop, perfect ? perfectDurationScale : 1f);

                gm.AdvanceWave();
                Announce($"{(perfect ? "PERFECT! " : "")}{(elite.IsBoss ? "BOSS" : "ELITE")} DOWN\n" +
                         $"CATCH THE DROP - WAVE {gm.Wave}", new Color(0.35f, 1f, 0.45f));
            }
            else
            {
                gm.AdvanceWave();
                Announce($"STOPPED IT - NO DROP\nWAVE {gm.Wave}", Color.white);
            }
        }

        private void SpawnPickup(int lane, Vector3 position, EffectType type, float durationScale)
        {
            var go = new GameObject($"Pickup_{type}");
            go.transform.SetParent(_container, false);
            go.transform.position = new Vector3(Lanes.GetLaneX(lane), position.y, 0f);
            go.transform.localScale = new Vector3(pickupSize, pickupSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = pickupColor;
            sr.sortingOrder = 7;

            go.AddComponent<BuffPickup>().Initialize(lane, type, durationScale, pickupSpeed, Lanes, player, this);
        }

        private void ClearElite()
        {
            if (!CurrentElite) return;
            CurrentElite.TimerExpired -= OnEliteTimerExpired;
            CurrentElite.Resolved -= OnEliteResolved;
            CurrentElite.Despawn();
            CurrentElite = null;
        }
    }
}
