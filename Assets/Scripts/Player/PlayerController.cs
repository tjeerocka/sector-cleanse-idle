using System;
using SectorCleanse.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SectorCleanse.Player
{
    /// <summary>
    /// Moves the player (and the squad parented to it) between lanes on the
    /// player line.
    ///
    /// Lane changes are LOGICALLY instant: <see cref="CurrentLane"/> updates the
    /// moment input is received, and <see cref="LaneChanged"/> fires right away
    /// (this is what lane effects such as "Compromised Position" react to).
    /// The transform then slides visually toward the lane centre.
    ///
    /// Input supported (works with both the new Input System and legacy Input Manager):
    ///  * Keyboard: A / D or Left / Right arrows (editor testing).
    ///  * Swipe left / right: step one lane.
    ///  * Tap: jump directly to the tapped lane.
    /// UI buttons can call <see cref="MoveLeft"/>, <see cref="MoveRight"/> or
    /// <see cref="MoveToLane"/> directly.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional. Falls back to LaneSystem.Instance.")]
        [SerializeField] private LaneSystem laneSystem;

        [Tooltip("Optional. Used to convert taps to world positions. Falls back to Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Header("Movement")]
        [Tooltip("Visual slide speed between lanes, in world units per second.")]
        [SerializeField, Min(0.1f)] private float laneSwitchSpeed = 30f;

        [Header("Touch input")]
        [Tooltip("Minimum horizontal drag (as a fraction of screen width) to count as a swipe.")]
        [SerializeField, Range(0.01f, 0.5f)] private float swipeThreshold = 0.08f;

        [Tooltip("If true, a tap (non-swipe) moves straight to the tapped lane.")]
        [SerializeField] private bool tapToMove = true;

        /// <summary>The lane the player currently occupies (0 = leftmost).</summary>
        public int CurrentLane { get; private set; }

        /// <summary>True while the transform is still sliding toward <see cref="CurrentLane"/>.</summary>
        public bool IsSliding { get; private set; }

        /// <summary>Raised with (previousLane, newLane) whenever the lane changes.</summary>
        public event Action<int, int> LaneChanged;

        private LaneSystem Lanes => laneSystem ? laneSystem : LaneSystem.Instance;

        private bool _pointerTracking;
        private Vector2 _pointerDownPosition;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void OnEnable()
        {
            // Subscribe in OnEnable (not Start): GameManager re-activates the gameplay
            // root right before raising RoundStarted, and OnEnable runs synchronously
            // during that activation whereas Start would be deferred to the next frame.
            if (GameManager.Instance) GameManager.Instance.RoundStarted += HandleRoundStarted;
        }

        private void OnDisable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted -= HandleRoundStarted;
            _pointerTracking = false;
        }

        private void Start()
        {
            SnapToLane(Lanes ? Lanes.CenterLane : 0);
        }

        private void Update()
        {
            if (!Lanes) return;

            // Only accept input while a round is running. If there is no GameManager
            // in the scene (isolated testing), movement is always allowed.
            bool canControl = !GameManager.Instance || GameManager.Instance.IsPlaying;
            if (canControl) ReadInput();

            SlideTowardCurrentLane();
        }

        // ------------------------------------------------------------------
        // Public movement API
        // ------------------------------------------------------------------

        public void MoveLeft() => MoveToLane(CurrentLane - 1);

        public void MoveRight() => MoveToLane(CurrentLane + 1);

        /// <summary>Request a move to <paramref name="lane"/>. Out-of-range lanes are clamped.</summary>
        public void MoveToLane(int lane)
        {
            if (!Lanes) return;

            int target = Lanes.ClampLane(lane);
            if (target == CurrentLane) return;

            int previous = CurrentLane;
            CurrentLane = target;
            LaneChanged?.Invoke(previous, CurrentLane);
        }

        /// <summary>Teleport to a lane with no slide (round start, respawn).</summary>
        public void SnapToLane(int lane)
        {
            if (!Lanes) return;

            int previous = CurrentLane;
            CurrentLane = Lanes.ClampLane(lane);
            transform.position = Lanes.GetPlayerPosition(CurrentLane);
            IsSliding = false;

            if (previous != CurrentLane) LaneChanged?.Invoke(previous, CurrentLane);
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private void HandleRoundStarted()
        {
            _pointerTracking = false;
            SnapToLane(Lanes ? Lanes.CenterLane : 0);
        }

        private void SlideTowardCurrentLane()
        {
            Vector3 target = Lanes.GetPlayerPosition(CurrentLane);
            transform.position = Vector3.MoveTowards(transform.position, target, laneSwitchSpeed * Time.deltaTime);
            IsSliding = (transform.position - target).sqrMagnitude > 0.0001f;
        }

        private void ReadInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) MoveLeft();
                if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) MoveRight();
            }

            // Pointer covers both mouse (editor) and the primary touch (device).
            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                if (pointer.press.wasPressedThisFrame) BeginPointer(pointer.position.ReadValue());
                if (pointer.press.wasReleasedThisFrame) EndPointer(pointer.position.ReadValue());
            }
#else
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) MoveLeft();
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) MoveRight();

            // Touches are mirrored to mouse button 0 by default (Input.simulateMouseWithTouches).
            if (Input.GetMouseButtonDown(0)) BeginPointer(Input.mousePosition);
            if (Input.GetMouseButtonUp(0)) EndPointer(Input.mousePosition);
#endif
        }

        private void BeginPointer(Vector2 screenPosition)
        {
            _pointerTracking = true;
            _pointerDownPosition = screenPosition;
        }

        private void EndPointer(Vector2 screenPosition)
        {
            if (!_pointerTracking) return;
            _pointerTracking = false;

            Vector2 delta = screenPosition - _pointerDownPosition;
            float minSwipePixels = swipeThreshold * Screen.width;

            // Horizontal swipe: step one lane in that direction.
            if (Mathf.Abs(delta.x) >= minSwipePixels && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                if (delta.x < 0f) MoveLeft();
                else MoveRight();
                return;
            }

            // Tap: jump to the lane under the finger.
            if (tapToMove) MoveToLane(ScreenToLane(screenPosition));
        }

        private int ScreenToLane(Vector2 screenPosition)
        {
            Camera cam = worldCamera ? worldCamera : Camera.main;
            if (!cam)
            {
                // No camera: split the screen evenly into lane-width columns.
                int column = Mathf.FloorToInt(screenPosition.x / Screen.width * Lanes.LaneCount);
                return Lanes.ClampLane(column);
            }

            // Distance from camera to the gameplay plane (needed for perspective cameras).
            float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
            return Lanes.GetNearestLane(world.x);
        }
    }
}
