using System;
using SectorCleanse.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SectorCleanse.Player
{
    /// <summary>
    /// Moves the player (and the squad parented to it) freely left and right along
    /// the player line, clamped to the outer edges of the lanes.
    ///
    /// Movement is continuous; lanes are not snapped to. <see cref="CurrentLane"/>
    /// is derived from the player's X position, and <see cref="LaneChanged"/> fires
    /// the moment the player crosses into another lane (this is what lane effects
    /// such as "Compromised Position" react to).
    ///
    /// Input supported (works with both the new Input System and legacy Input Manager):
    ///  * Keyboard: hold A / D or Left / Right arrows.
    ///  * Mouse / touch: press and drag horizontally anywhere on screen. Movement is
    ///    relative to the drag, so the finger never has to cover the player.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional. Falls back to LaneSystem.Instance.")]
        [SerializeField] private LaneSystem laneSystem;

        [Tooltip("Optional. Used to convert drags to world positions. Falls back to Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Header("Movement")]
        [Tooltip("Speed when steering with the keyboard, in world units per second.")]
        [SerializeField, Min(0.1f)] private float keyboardSpeed = 10f;

        [Tooltip("Drag multiplier. 1 = the player moves exactly as far as the finger.")]
        [SerializeField, Min(0.1f)] private float dragSensitivity = 1.2f;

        [Tooltip("Half the player's width; keeps the player fully inside the outer lanes.")]
        [SerializeField, Min(0f)] private float edgePadding = 0.4f;

        /// <summary>The lane the player is currently standing in (0 = leftmost).</summary>
        public int CurrentLane { get; private set; }

        /// <summary>Raised with (previousLane, newLane) whenever the player crosses into another lane.</summary>
        public event Action<int, int> LaneChanged;

        private LaneSystem Lanes => laneSystem ? laneSystem : LaneSystem.Instance;

        private bool _dragging;
        private float _lastPointerWorldX;

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
            _dragging = false;
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
            if (!canControl)
            {
                _dragging = false;
                return;
            }

            float x = transform.position.x;
            x += ReadKeyboardAxis() * keyboardSpeed * Time.deltaTime;
            x += ReadDragDelta() * dragSensitivity;
            SetX(x);
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Teleport to the centre of a lane (round start, respawn).</summary>
        public void SnapToLane(int lane)
        {
            if (!Lanes) return;
            _dragging = false;
            SetX(Lanes.GetLaneX(lane));
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private void HandleRoundStarted() => SnapToLane(Lanes ? Lanes.CenterLane : 0);

        private void SetX(float x)
        {
            float min = Lanes.LeftEdgeX + edgePadding;
            float max = Lanes.RightEdgeX - edgePadding;
            x = Mathf.Clamp(x, min, max);

            transform.position = new Vector3(x, Lanes.PlayerLineWorldY, transform.position.z);

            int lane = Lanes.GetNearestLane(x);
            if (lane == CurrentLane) return;

            int previous = CurrentLane;
            CurrentLane = lane;
            LaneChanged?.Invoke(previous, lane);
        }

        /// <summary>-1 (left) .. +1 (right) from held keys.</summary>
        private static float ReadKeyboardAxis()
        {
            float axis = 0f;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) axis -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) axis += 1f;
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) axis -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) axis += 1f;
#endif
            return axis;
        }

        /// <summary>World-space horizontal distance the pointer moved this frame while held.</summary>
        private float ReadDragDelta()
        {
            bool pressed;
            Vector2 screenPosition;
#if ENABLE_INPUT_SYSTEM
            // Pointer covers both mouse (editor) and the primary touch (device).
            Pointer pointer = Pointer.current;
            if (pointer == null) return 0f;
            pressed = pointer.press.isPressed;
            screenPosition = pointer.position.ReadValue();
#else
            // Touches are mirrored to mouse button 0 by default (Input.simulateMouseWithTouches).
            pressed = Input.GetMouseButton(0);
            screenPosition = Input.mousePosition;
#endif
            if (!pressed)
            {
                _dragging = false;
                return 0f;
            }

            float pointerX = ScreenToWorldX(screenPosition);
            if (!_dragging)
            {
                // First frame of the drag: just anchor, don't move.
                _dragging = true;
                _lastPointerWorldX = pointerX;
                return 0f;
            }

            float delta = pointerX - _lastPointerWorldX;
            _lastPointerWorldX = pointerX;
            return delta;
        }

        private float ScreenToWorldX(Vector2 screenPosition)
        {
            Camera cam = worldCamera ? worldCamera : Camera.main;
            if (!cam)
            {
                // No camera: map the screen width onto the lane area.
                float t = screenPosition.x / Screen.width;
                return Mathf.Lerp(Lanes.LeftEdgeX, Lanes.RightEdgeX, t);
            }

            // Distance from camera to the gameplay plane (needed for perspective cameras).
            float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
            return cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth)).x;
        }
    }
}
