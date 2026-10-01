using System;
using System.Collections;
using UnityEngine;

namespace SectorCleanse.Core
{
    /// <summary>
    /// Owns the top-level game loop:
    ///
    ///   MainMenu --StartRound()--> Playing --EndRound()--> GameOver --(delay)--> MainMenu
    ///
    /// Responsibilities are deliberately narrow:
    ///  * track and broadcast the current <see cref="GameState"/>;
    ///  * toggle the menu / gameplay / game-over scene roots;
    ///  * track money earned during a round and bank it when the round ends.
    ///
    /// Gameplay systems (player, squad, spawner, weapons) do NOT get called by the
    /// GameManager directly. They subscribe to <see cref="RoundStarted"/> /
    /// <see cref="RoundEnded"/> and check <see cref="IsPlaying"/>, which keeps them
    /// decoupled and easy to add or remove.
    ///
    /// Graybox setup uses a single scene: everything for the round lives under
    /// <see cref="gameplayRoot"/>, the start menu lives under <see cref="menuRoot"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Initialise before gameplay scripts so Instance is ready in their Awake/Start.
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        private const string BankedMoneyKey = "SectorCleanse.BankedMoney";

        public static GameManager Instance { get; private set; }

        [Header("Scene roots (toggled per state)")]
        [Tooltip("Start menu UI / upgrade shop. Active only in MainMenu.")]
        [SerializeField] private GameObject menuRoot;

        [Tooltip("Everything that exists during a round (player, lanes, spawners, HUD). Active in Playing and GameOver.")]
        [SerializeField] private GameObject gameplayRoot;

        [Tooltip("Optional 'You died' overlay. Active only in GameOver.")]
        [SerializeField] private GameObject gameOverRoot;

        [Header("Flow")]
        [Tooltip("Seconds the game-over overlay stays up before returning to the start menu.")]
        [SerializeField, Min(0f)] private float gameOverDelay = 2f;

        [Tooltip("Start a round immediately on Play (skips the menu). Handy while grayboxing.")]
        [SerializeField] private bool autoStartRound;

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        public GameState State { get; private set; } = GameState.MainMenu;
        public bool IsPlaying => State == GameState.Playing;

        /// <summary>Seconds survived in the current (or last) round.</summary>
        public float RoundTime { get; private set; }

        /// <summary>Money earned in the current round (not yet banked).</summary>
        public int RoundMoney { get; private set; }

        /// <summary>Persistent money, spent in the start menu on permanent upgrades.</summary>
        public int BankedMoney { get; private set; }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        public event Action<GameState> StateChanged;
        public event Action RoundStarted;
        public event Action<RoundResult> RoundEnded;
        public event Action<int> RoundMoneyChanged;
        public event Action<int> BankedMoneyChanged;

        private Coroutine _returnToMenuRoutine;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Duplicate {nameof(GameManager)} on '{name}' destroyed.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BankedMoney = PlayerPrefs.GetInt(BankedMoneyKey, 0);
        }

        private void Start()
        {
            // Apply the initial state's scene roots, then optionally jump straight into a round.
            SetState(GameState.MainMenu);
            if (autoStartRound) StartRound();
        }

        private void Update()
        {
            if (IsPlaying) RoundTime += Time.deltaTime;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // Game loop API
        // ------------------------------------------------------------------

        /// <summary>Begin a new round. Hook this to the menu's "Deploy" button.</summary>
        public void StartRound()
        {
            if (IsPlaying) return;

            if (_returnToMenuRoutine != null)
            {
                StopCoroutine(_returnToMenuRoutine);
                _returnToMenuRoutine = null;
            }

            RoundTime = 0f;
            RoundMoney = 0;
            RoundMoneyChanged?.Invoke(RoundMoney);

            // Activate gameplay objects first so their listeners are live, then announce the round.
            SetState(GameState.Playing);
            RoundStarted?.Invoke();
        }

        /// <summary>
        /// End the current round (squad wiped). Banks the round's money and returns
        /// to the start menu after <see cref="gameOverDelay"/>.
        /// </summary>
        public void EndRound()
        {
            if (!IsPlaying) return;

            var result = new RoundResult(RoundTime, RoundMoney);
            AddBankedMoney(RoundMoney);

            SetState(GameState.GameOver);
            RoundEnded?.Invoke(result);

            _returnToMenuRoutine = StartCoroutine(ReturnToMenuAfterDelay());
        }

        /// <summary>Immediately show the start menu (e.g. a "skip" button on the game-over overlay).</summary>
        public void ReturnToMenu()
        {
            if (_returnToMenuRoutine != null)
            {
                StopCoroutine(_returnToMenuRoutine);
                _returnToMenuRoutine = null;
            }
            if (IsPlaying) EndRound(); // Never abandon a round without banking its money.
            SetState(GameState.MainMenu);
        }

        // ------------------------------------------------------------------
        // Economy API
        // ------------------------------------------------------------------

        /// <summary>Award money during a round (e.g. on enemy kill).</summary>
        public void AddRoundMoney(int amount)
        {
            if (!IsPlaying || amount <= 0) return;
            RoundMoney += amount;
            RoundMoneyChanged?.Invoke(RoundMoney);
        }

        /// <summary>Spend banked money on a permanent upgrade. Returns false if unaffordable.</summary>
        public bool TrySpendBankedMoney(int cost)
        {
            if (cost < 0 || BankedMoney < cost) return false;
            AddBankedMoney(-cost);
            return true;
        }

        private void AddBankedMoney(int delta)
        {
            if (delta == 0) return;
            BankedMoney += delta;
            PlayerPrefs.SetInt(BankedMoneyKey, BankedMoney);
            PlayerPrefs.Save();
            BankedMoneyChanged?.Invoke(BankedMoney);
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private IEnumerator ReturnToMenuAfterDelay()
        {
            // Realtime so the delay still works if something pauses Time.timeScale on death.
            yield return new WaitForSecondsRealtime(gameOverDelay);
            _returnToMenuRoutine = null;
            SetState(GameState.MainMenu);
        }

        private void SetState(GameState newState)
        {
            State = newState;

            if (menuRoot) menuRoot.SetActive(newState == GameState.MainMenu);
            if (gameplayRoot) gameplayRoot.SetActive(newState != GameState.MainMenu);
            if (gameOverRoot) gameOverRoot.SetActive(newState == GameState.GameOver);

            StateChanged?.Invoke(newState);
        }
    }
}
