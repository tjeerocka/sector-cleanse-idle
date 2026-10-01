using System.Collections.Generic;
using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;

namespace SectorCleanse.Combat
{
    /// <summary>
    /// Spawns enemies at the top of random lanes while a round is running.
    /// Difficulty ramps over the round: enemies spawn more often and get more HP.
    /// A lane only gets a new enemy once the previous one has moved clear of the
    /// spawn point, so dense spawning never stacks enemies on top of each other.
    ///
    /// Graybox: enemies are built in code from a sprite, so no prefabs are needed.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySpawner : MonoBehaviour
    {
        [Header("References (optional, fall back to scene singletons / search)")]
        [SerializeField] private LaneSystem laneSystem;
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerSquad squad;

        [Header("Spawn rate")]
        [Tooltip("Seconds between spawns at the start of a round.")]
        [SerializeField, Min(0.05f)] private float startInterval = 0.4f;

        [Tooltip("Fastest spawn interval, reached after Ramp Duration seconds.")]
        [SerializeField, Min(0.05f)] private float minInterval = 0.15f;

        [Tooltip("Seconds until the spawn interval reaches its minimum.")]
        [SerializeField, Min(1f)] private float rampDuration = 300f;

        [Tooltip("Delay before the first enemy of a round.")]
        [SerializeField, Min(0f)] private float firstSpawnDelay = 1f;

        [Header("Enemies")]
        [SerializeField, Min(0.1f)] private float enemySpeed = 1.5f;
        [SerializeField, Min(1)] private int baseHp = 3;

        [Tooltip("Extra HP added per second of round time (0.1 = +1 HP every 10 s).")]
        [SerializeField, Min(0f)] private float hpPerSecond = 0.1f;

        [Tooltip("Random extra HP, 0..this value, added to each enemy.")]
        [SerializeField, Min(0)] private int hpVariance = 2;

        [SerializeField, Min(0.1f)] private float enemySize = 0.9f;
        [SerializeField] private Sprite enemySprite;
        [SerializeField] private Color enemyColor = new Color(0.9f, 0.25f, 0.25f);

        [Tooltip("Extra empty space (world units) between two enemies in the same lane.")]
        [SerializeField, Min(0f)] private float laneGap = 0.15f;

        private Transform _container;
        private float _timer;
        private readonly List<float> _laneFreeAt = new List<float>();
        private readonly List<int> _freeLanes = new List<int>();

        private LaneSystem Lanes => laneSystem ? laneSystem : LaneSystem.Instance;

        private float RoundTime => GameManager.Instance ? GameManager.Instance.RoundTime : Time.timeSinceLevelLoad;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _container = new GameObject("Enemies").transform;
            _container.SetParent(transform, false);

            if (!player) player = FindAnyObjectByType<PlayerController>();
            if (!squad) squad = FindAnyObjectByType<PlayerSquad>();
            _timer = firstSpawnDelay;
        }

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
            if (!Lanes) return;
            if (GameManager.Instance && !GameManager.Instance.IsPlaying) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            int lane = PickFreeLane();
            if (lane < 0) return; // Every lane still has an enemy at the spawn point; try next frame.

            Spawn(lane, CurrentHp());
            _laneFreeAt[lane] = Time.time + (enemySize + laneGap) / enemySpeed;
            _timer = CurrentInterval();
        }

        // ------------------------------------------------------------------
        // Difficulty curve
        // ------------------------------------------------------------------

        private float CurrentInterval() =>
            Mathf.Lerp(startInterval, minInterval, RoundTime / rampDuration);

        private int CurrentHp() =>
            baseHp + Mathf.FloorToInt(RoundTime * hpPerSecond) + Random.Range(0, hpVariance + 1);

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        /// <summary>Random lane whose previous enemy has moved clear of the spawn point, or -1.</summary>
        private int PickFreeLane()
        {
            while (_laneFreeAt.Count < Lanes.LaneCount) _laneFreeAt.Add(0f);

            _freeLanes.Clear();
            for (int i = 0; i < Lanes.LaneCount; i++)
                if (Time.time >= _laneFreeAt[i]) _freeLanes.Add(i);

            return _freeLanes.Count == 0 ? -1 : _freeLanes[Random.Range(0, _freeLanes.Count)];
        }

        private void ResetForRound()
        {
            _timer = firstSpawnDelay;
            _laneFreeAt.Clear();
            for (int i = _container.childCount - 1; i >= 0; i--)
            {
                var enemy = _container.GetChild(i).GetComponent<Enemy>();
                if (enemy) enemy.Despawn();
                else Destroy(_container.GetChild(i).gameObject);
            }
        }

        private void Spawn(int lane, int hp)
        {
            var go = new GameObject($"Enemy_L{lane}_HP{hp}");
            go.transform.SetParent(_container, false);
            go.transform.position = Lanes.GetSpawnPosition(lane);
            go.transform.localScale = new Vector3(enemySize, enemySize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = enemySprite;
            sr.color = enemyColor;
            sr.sortingOrder = 5;

            go.AddComponent<Enemy>().Initialize(lane, hp, enemySpeed, Lanes, player, squad);
        }
    }
}
