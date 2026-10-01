using System;
using SectorCleanse.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SectorCleanse.Combat
{
    /// <summary>
    /// The "hold to fire" gun button. Auto-fire always runs; holding the button
    /// (the on-screen FIRE button, or Space in the editor) boosts it:
    ///  * fire rate and damage × <see cref="FireRateBoost"/> / <see cref="DamageBoost"/>
    ///    for the player and every soldier;
    ///  * kills made by boosted bullets pay × <see cref="MoneyBoost"/>;
    ///  * only boosted bullets can hurt elites.
    ///
    /// Holding builds heat. Above <see cref="hotThreshold"/> the button warns (HOT!);
    /// at full heat the gun jams for <see cref="jamDuration"/> seconds and the whole
    /// squad stops firing (auto-fire too). Releasing cools it down, so short bursts
    /// beat holding the button forever.
    /// </summary>
    [DisallowMultipleComponent]
    public class ManualFire : MonoBehaviour
    {
        public const float FireRateBoost = 2f;
        public const float DamageBoost = 2f;
        public const float MoneyBoost = 2f;

        [Tooltip("Heat gained per second while firing (1 = full). 0.2 = jams after 5 s of holding.")]
        [SerializeField, Min(0.01f)] private float heatPerSecond = 0.2f;

        [Tooltip("Heat lost per second while not firing.")]
        [SerializeField, Min(0.01f)] private float coolPerSecond = 0.4f;

        [Tooltip("Seconds the whole squad can't fire after overheating.")]
        [SerializeField, Min(0f)] private float jamDuration = 1.5f;

        [Tooltip("Heat (0..1) above which the FIRE button warns that a jam is close.")]
        [SerializeField, Range(0f, 1f)] private float hotThreshold = 0.8f;

        /// <summary>The player wants to fire (button or key held).</summary>
        public bool IsHeld => _buttonHeld || ReadKey();

        /// <summary>The boost is active right now (held, not jammed, round running).</summary>
        public bool IsFiring { get; private set; }

        /// <summary>0..1 heat; the gun jams at 1.</summary>
        public float Heat { get; private set; }

        public bool IsJammed => _jamTimer > 0f;

        /// <summary>Close to jamming: let go soon.</summary>
        public bool IsHot => !IsJammed && Heat >= hotThreshold;

        /// <summary>Seconds of jam left (0 when not jammed).</summary>
        public float JamRemaining => Mathf.Max(0f, _jamTimer);

        public float DamageMultiplier => IsFiring ? DamageBoost : 1f;
        public float FireRateMultiplier => IsFiring ? FireRateBoost : 1f;

        /// <summary>Raised when the gun overheats and jams.</summary>
        public event Action Overheated;

        private bool _buttonHeld;
        private float _jamTimer;

        private void OnEnable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted += ResetForRound;
        }

        private void OnDisable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted -= ResetForRound;
            _buttonHeld = false;
            IsFiring = false;
        }

        private void Update()
        {
            if (GameManager.Instance && !GameManager.Instance.IsPlaying)
            {
                IsFiring = false;
                return;
            }

            float dt = Time.deltaTime;
            if (_jamTimer > 0f)
            {
                _jamTimer -= dt;
                Heat = Mathf.Max(0f, Heat - coolPerSecond * dt);
                IsFiring = false;
                return;
            }

            IsFiring = IsHeld;
            if (IsFiring)
            {
                Heat += heatPerSecond * dt;
                if (Heat >= 1f)
                {
                    Heat = 1f;
                    _jamTimer = jamDuration;
                    IsFiring = false;
                    Overheated?.Invoke();
                }
            }
            else
            {
                Heat = Mathf.Max(0f, Heat - coolPerSecond * dt);
            }
        }

        /// <summary>Called by the on-screen FIRE button.</summary>
        public void SetButtonHeld(bool held) => _buttonHeld = held;

        private void ResetForRound()
        {
            Heat = 0f;
            _jamTimer = 0f;
            IsFiring = false;
        }

        private static bool ReadKey()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.isPressed;
#else
            return Input.GetKey(KeyCode.Space);
#endif
        }
    }
}
