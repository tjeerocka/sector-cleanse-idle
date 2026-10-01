using System;
using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;

namespace SectorCleanse.Combat
{
    /// <summary>
    /// The elite that closes every wave (spawned by <see cref="WaveDirector"/>).
    ///
    ///  * Entering: drops in from the top of its lane to its hover point.
    ///  * Hovering: a countdown runs. Only <b>manual</b> bullets (fired while holding
    ///    FIRE) hurt it; auto-fire bounces off with an "IMMUNE" flash, so it can never
    ///    be killed idly.
    ///  * Marching: the countdown ran out. It walks down its lane; reaching the player
    ///    line while you stand in its lane deals its remaining HP to the squad
    ///    (practically a wipe). It can still be killed on the way down.
    /// </summary>
    public class Elite : Enemy
    {
        public enum Phase { Entering, Hovering, Marching }

        public Phase State { get; private set; }
        public bool IsBoss { get; private set; }
        public float TimeLimit { get; private set; }
        public float TimeLeft { get; private set; }

        /// <summary>Fraction of the countdown left (1 = untouched, 0 = expired).</summary>
        public float TimeLeftFraction => TimeLimit > 0f ? TimeLeft / TimeLimit : 0f;

        /// <summary>The countdown ran out (the elite starts marching).</summary>
        public event Action<Elite> TimerExpired;

        /// <summary>The elite is gone: true = killed, false = broke through the player line.</summary>
        public event Action<Elite, bool> Resolved;

        private float _hoverY;
        private float _marchSpeed;
        private float _rewardMultiplier;
        private float _immuneFlash;
        private TextMesh _timerLabel;
        private TextMesh _immuneLabel;
        private int _shownSeconds = -1;

        public void Setup(int lane, double hp, LaneSystem lanes, PlayerController player,
            PlayerSquad squad, bool boss, float timeLimit, float hoverY, float enterSpeed,
            float marchSpeed, float rewardMultiplier)
        {
            Initialize(lane, hp, enterSpeed, lanes, player, squad);
            IsBoss = boss;
            TimeLimit = TimeLeft = Mathf.Max(1f, timeLimit);
            _hoverY = hoverY;
            _marchSpeed = marchSpeed;
            _rewardMultiplier = rewardMultiplier;
            State = Phase.Entering;

            _timerLabel = CreateWorldText("Timer", new Vector3(0f, 0.72f, 0f), 0.045f, new Color(1f, 0.85f, 0.3f));
            _immuneLabel = CreateWorldText("Immune", new Vector3(0f, -0.72f, 0f), 0.035f, new Color(0.8f, 0.8f, 0.9f));
            _immuneLabel.text = "";
            RefreshTimerLabel();
        }

        protected override void Tick(float dt)
        {
            switch (State)
            {
                case Phase.Entering:
                    transform.position += Vector3.down * (Speed * dt);
                    if (transform.position.y <= _hoverY)
                    {
                        Vector3 p = transform.position;
                        transform.position = new Vector3(p.x, _hoverY, p.z);
                        State = Phase.Hovering;
                    }
                    break;

                case Phase.Hovering:
                    TimeLeft -= dt;
                    if (TimeLeft <= 0f)
                    {
                        TimeLeft = 0f;
                        State = Phase.Marching;
                        TimerExpired?.Invoke(this);
                    }
                    break;

                case Phase.Marching:
                    transform.position += Vector3.down * (_marchSpeed * dt);
                    if (transform.position.y <= Lanes.PlayerLineWorldY) ReachPlayerLine();
                    break;
            }

            if (_immuneFlash > 0f)
            {
                _immuneFlash -= dt;
                if (_immuneFlash <= 0f && _immuneLabel) _immuneLabel.text = "";
            }
            RefreshTimerLabel();
        }

        /// <summary>Auto-fire bullets are absorbed without effect; only manual bullets hurt.</summary>
        public override void TakeHit(double amount, bool manual)
        {
            if (IsDead) return;
            if (manual)
            {
                TakeDamage(amount, true);
                return;
            }

            _immuneFlash = 0.4f;
            if (_immuneLabel) _immuneLabel.text = "IMMUNE - HOLD FIRE";
        }

        protected override void OnKilled(bool manual)
        {
            if (GameManager.Instance) GameManager.Instance.AddKillReward(_rewardMultiplier);
            Resolved?.Invoke(this, true);
        }

        protected override void ReachPlayerLine()
        {
            base.ReachPlayerLine(); // Hits the squad if you're in its lane, then despawns.
            Resolved?.Invoke(this, false);
        }

        private void RefreshTimerLabel()
        {
            if (!_timerLabel) return;
            if (State == Phase.Marching)
            {
                if (_shownSeconds == -2) return;
                _shownSeconds = -2;
                _timerLabel.text = "BREAKING THROUGH!";
                _timerLabel.color = new Color(1f, 0.35f, 0.3f);
                return;
            }

            int seconds = Mathf.CeilToInt(TimeLeft);
            if (seconds == _shownSeconds) return;
            _shownSeconds = seconds;
            _timerLabel.text = $"{(IsBoss ? "BOSS" : "ELITE")} {seconds}s";
        }
    }
}
