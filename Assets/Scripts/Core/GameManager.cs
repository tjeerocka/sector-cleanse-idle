using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SectorCleanse.Core
{
    /// <summary>
    /// Owns the top-level game loop:
    ///
    ///   MainMenu --StartRound()/ContinueRun()--> Playing --EndRound()--> GameOver --(delay)--> MainMenu
    ///                                             Playing --SuspendRun()--> MainMenu (run saved)
    ///
    /// Saved runs: while playing, the run is autosaved every few seconds and whenever
    /// the app is paused/closed. The menu then offers CONTINUE (restores time, wave,
    /// round money and the surviving squad) or ABANDON (banks the round money).
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

        [Tooltip("Seconds per wave. Waves are a display of difficulty progress (difficulty ramps with round time).")]
        [SerializeField, Min(1f)] private float waveDuration = 20f;

        [Tooltip("Seconds between autosaves while a round is running.")]
        [SerializeField, Min(1f)] private float autosaveInterval = 3f;

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

        /// <summary>Result of the most recently finished round (read by the game-over screen).</summary>
        public RoundResult LastRoundResult { get; private set; }

        /// <summary>Current wave (1-based), derived from round time.</summary>
        public int Wave => WaveAt(RoundTime);

        /// <summary>Wave number for a given round time (also used to describe saved runs).</summary>
        public int WaveAt(float roundTime) => 1 + Mathf.FloorToInt(roundTime / waveDuration);

        /// <summary>True when a suspended/interrupted run is waiting to be continued.</summary>
        public bool HasSavedRun => RunSave.Exists;

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        public event Action<GameState> StateChanged;
        public event Action RoundStarted;
        public event Action<RoundResult> RoundEnded;
        public event Action<int> RoundMoneyChanged;
        public event Action<int> BankedMoneyChanged;

        /// <summary>Raised when a saved run appears or disappears (menu swaps DEPLOY / CONTINUE).</summary>
        public event Action SavedRunChanged;

        private Coroutine _returnToMenuRoutine;
        private float _autosaveTimer;

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
            if (!IsPlaying) return;

            RoundTime += Time.deltaTime;

            _autosaveTimer -= Time.unscaledDeltaTime;
            if (_autosaveTimer <= 0f) SaveRun();
        }

        // Mobile: the app goes to the background (home button, call, lock screen).
        private void OnApplicationPause(bool paused)
        {
            if (paused && IsPlaying) SaveRun();
        }

        // Desktop / editor: window closed or Play mode stopped.
        private void OnApplicationQuit()
        {
            if (IsPlaying) SaveRun();
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
            if (HasSavedRun) AbandonRun(); // A new run replaces any suspended one (its money is banked).
            BeginRound(null);
        }

        /// <summary>Resume the saved run (menu "Continue" button).</summary>
        public void ContinueRun()
        {
            if (IsPlaying) return;
            RunSaveData data = RunSave.Read();
            if (data == null)
            {
                SavedRunChanged?.Invoke();
                return;
            }
            BeginRound(data);
        }

        /// <summary>Give up the saved run: its round money is banked, the run is discarded.</summary>
        public void AbandonRun()
        {
            RunSaveData data = RunSave.Read();
            if (data != null) AddBankedMoney(data.roundMoney);
            DeleteSavedRun();
        }

        /// <summary>Pause button: save the run and go to the menu without ending it.</summary>
        public void SuspendRun()
        {
            if (!IsPlaying) return;
            SaveRun();
            SetState(GameState.MainMenu);
        }

        private void BeginRound(RunSaveData resume)
        {
            if (_returnToMenuRoutine != null)
            {
                StopCoroutine(_returnToMenuRoutine);
                _returnToMenuRoutine = null;
            }

            RoundTime = resume?.roundTime ?? 0f;
            RoundMoney = resume?.roundMoney ?? 0;
            RoundMoneyChanged?.Invoke(RoundMoney);
            _autosaveTimer = autosaveInterval;

            // Activate gameplay objects first so their listeners are live, then announce the round.
            SetState(GameState.Playing);
            RoundStarted?.Invoke();

            // Continuing: components reset normally above, then restore their saved state.
            if (resume == null) return;
            var participants = new List<IRunStatePersistent>(RunSave.Registered);
            foreach (IRunStatePersistent participant in participants) participant.LoadRunState(resume);
        }

        /// <summary>Snapshot the running round to storage.</summary>
        private void SaveRun()
        {
            _autosaveTimer = autosaveInterval;
            if (!IsPlaying) return;

            var data = new RunSaveData { roundTime = RoundTime, roundMoney = RoundMoney };
            foreach (IRunStatePersistent participant in RunSave.Registered) participant.SaveRunState(data);

            bool hadSave = RunSave.Exists;
            RunSave.Write(data);
            if (!hadSave) SavedRunChanged?.Invoke();
        }

        private void DeleteSavedRun()
        {
            if (!RunSave.Exists) return;
            RunSave.Delete();
            SavedRunChanged?.Invoke();
        }

        /// <summary>
        /// End the current round (squad wiped). Banks the round's money and returns
        /// to the start menu after <see cref="gameOverDelay"/>.
        /// </summary>
        public void EndRound()
        {
            if (!IsPlaying) return;

            var result = new RoundResult(RoundTime, RoundMoney, Wave);
            LastRoundResult = result;
            AddBankedMoney(RoundMoney);
            DeleteSavedRun(); // The run is over; nothing to continue.

            SetState(GameState.GameOver);
            RoundEnded?.Invoke(result);

            _returnToMenuRoutine = StartCoroutine(ReturnToMenuAfterDelay());
        }

        /// <summary>Immediately show the start menu (e.g. a "skip" button on the game-over overlay).</summary>
        public void ReturnToMenu()
        {
            if (IsPlaying) EndRound(); // Never abandon a round without banking its money.
            if (_returnToMenuRoutine != null)
            {
                StopCoroutine(_returnToMenuRoutine);
                _returnToMenuRoutine = null;
            }
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

#if UNITY_EDITOR
        // Right-click the component header to test the shop without grinding rounds.
        [ContextMenu("Debug/Add $100 to bank")]
        private void DebugAddMoney() => AddBankedMoney(100);

        [ContextMenu("Debug/Reset bank to $0")]
        private void DebugResetMoney() => AddBankedMoney(-BankedMoney);
#endif

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
