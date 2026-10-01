namespace SectorCleanse.Core
{
    /// <summary>
    /// High-level states of the game loop. GameManager owns the current state;
    /// every other system reads it (or listens to GameManager.StateChanged).
    /// </summary>
    public enum GameState
    {
        /// <summary>Start menu is shown. Meta-upgrades are purchased here.</summary>
        MainMenu,

        /// <summary>A round is running: lanes spawn, the squad fights.</summary>
        Playing,

        /// <summary>The squad was wiped. Short pause before returning to the menu.</summary>
        GameOver
    }

    /// <summary>
    /// Summary of a finished round, passed to GameManager.RoundEnded listeners
    /// (e.g. a game-over panel, analytics, achievements).
    /// </summary>
    public readonly struct RoundResult
    {
        public readonly float SurvivalTime;
        public readonly int MoneyEarned;
        public readonly int Wave;

        public RoundResult(float survivalTime, int moneyEarned, int wave)
        {
            SurvivalTime = survivalTime;
            MoneyEarned = moneyEarned;
            Wave = wave;
        }
    }
}
