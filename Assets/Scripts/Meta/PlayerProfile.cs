using System;
using System.Text.RegularExpressions;
using SectorCleanse.Core;
using UnityEngine;

namespace SectorCleanse.Meta
{
    /// <summary>
    /// The player's name and their link to the leaderboard.
    ///
    /// * A name is required before playing (the name screen shows until one is claimed).
    /// * Rules: <see cref="MinLength"/>–<see cref="MaxLength"/> characters, letters A–Z
    ///   and digits 0–9 only, unique (case-insensitive) on the leaderboard.
    /// * Every finished or abandoned run is submitted; the board keeps the player's best.
    /// </summary>
    [DefaultExecutionOrder(-92)] // Ready before the menu UI reads it.
    [DisallowMultipleComponent]
    public class PlayerProfile : MonoBehaviour
    {
        public const int MinLength = 3;
        public const int MaxLength = 12;
        private const string NameKey = "SectorCleanse.PlayerName";

        private static readonly Regex AllowedName = new Regex("^[A-Za-z0-9]+$");

        public string PlayerName { get; private set; }
        public bool HasName => !string.IsNullOrEmpty(PlayerName);

        public ILeaderboardService Leaderboard { get; private set; }

        /// <summary>Raised after a name is claimed.</summary>
        public event Action NameChanged;

        /// <summary>Raised after a score was submitted (ranks may have changed).</summary>
        public event Action ScoresChanged;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            Leaderboard = CreateLeaderboardService();
            PlayerName = PlayerPrefs.GetString(NameKey, "");
        }

        private void OnEnable()
        {
            if (GameManager.Instance) GameManager.Instance.RunScored += SubmitRun;
        }

        private void OnDisable()
        {
            if (GameManager.Instance) GameManager.Instance.RunScored -= SubmitRun;
        }

        /// <summary>Swap this for an online implementation when a backend exists.</summary>
        protected virtual ILeaderboardService CreateLeaderboardService() => new LocalLeaderboardService();

        // ------------------------------------------------------------------
        // Names
        // ------------------------------------------------------------------

        /// <summary>Checks the format rules. Returns null if valid, otherwise a message for the player.</summary>
        public static string ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < MinLength)
                return $"NAME MUST BE AT LEAST {MinLength} CHARACTERS";
            if (name.Length > MaxLength)
                return $"NAME CAN BE AT MOST {MaxLength} CHARACTERS";
            if (!AllowedName.IsMatch(name))
                return "ONLY LETTERS A-Z AND DIGITS 0-9";
            return null;
        }

        /// <summary>
        /// Validate and claim a name. Callback: (success, error message or null).
        /// </summary>
        public void TryClaimName(string rawName, Action<bool, string> done)
        {
            string name = (rawName ?? "").Trim();
            string error = ValidateName(name);
            if (error != null)
            {
                done?.Invoke(false, error);
                return;
            }

            Leaderboard.RegisterName(name, ok =>
            {
                if (!ok)
                {
                    done?.Invoke(false, "THAT NAME IS TAKEN");
                    return;
                }

                PlayerName = name;
                PlayerPrefs.SetString(NameKey, PlayerName);
                PlayerPrefs.Save();

                // Carry over bests earned before names existed.
                GameManager gm = GameManager.Instance;
                if (gm && (gm.BestWave > 0 || gm.BestRunMoney > 0))
                    Leaderboard.SubmitScore(PlayerName, gm.BestWave, gm.BestRunMoney);

                NameChanged?.Invoke();
                ScoresChanged?.Invoke();
                done?.Invoke(true, null);
            });
        }

        // ------------------------------------------------------------------
        // Scores
        // ------------------------------------------------------------------

        private void SubmitRun(int wave, int runMoney)
        {
            if (!HasName) return;
            Leaderboard.SubmitScore(PlayerName, wave, runMoney, () => ScoresChanged?.Invoke());
        }

        /// <summary>Convenience: this player's rank (0 = unranked / no name).</summary>
        public void GetOwnRank(Action<int> callback)
        {
            if (!HasName)
            {
                callback?.Invoke(0);
                return;
            }
            Leaderboard.GetRank(PlayerName, callback);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Add 8 sample rivals to leaderboard")]
        private void DebugAddRivals()
        {
            string[] names = { "Viper", "Nova7", "Ghost", "Atlas", "Rook42", "Echo", "Blaze9", "Titan" };
            var random = new System.Random();
            foreach (string rival in names)
            {
                Leaderboard.RegisterName(rival, _ => { });
                Leaderboard.SubmitScore(rival, random.Next(1, 40), random.Next(50, 5000));
            }
            ScoresChanged?.Invoke();
        }

        [ContextMenu("Debug/Forget player name (shows name screen again)")]
        private void DebugForgetName()
        {
            PlayerName = "";
            PlayerPrefs.DeleteKey(NameKey);
            PlayerPrefs.Save();
            NameChanged?.Invoke();
        }

        [ContextMenu("Debug/Clear leaderboard")]
        private void DebugClearLeaderboard()
        {
            PlayerPrefs.DeleteKey("SectorCleanse.Leaderboard");
            PlayerPrefs.Save();
            Leaderboard = CreateLeaderboardService();
            ScoresChanged?.Invoke();
        }
#endif
    }
}
