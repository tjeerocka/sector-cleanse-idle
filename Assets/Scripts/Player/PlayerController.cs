using System;
using System.Collections.Generic;
using SectorCleanse.Core;
using UnityEngine;
using UnityEngine.EventSystems;
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
    ///
    /// Multi-touch: every finger is tracked separately. A finger that goes down on a
    /// button (FIRE, pause) never steers, so you can drag with one thumb while
    /// holding FIRE with the other.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour, IRunStatePersistent
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

        private const int NoPointer = int.MinValue;
        private const int MousePointer = -1;

        private struct PointerSample
        {
            public int Id;
            public Vector2 Position;
            public bool Began;
        }

        private readonly List<PointerSample> _pointers = new List<PointerSample>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private int _dragPointer = NoPointer;
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
            RunSave.Register(this);
        }

        private void OnDisable()
        {
            if (GameManager.Instance) GameManager.Instance.RoundStarted -= HandleRoundStarted;
            RunSave.Unregister(this);
            _dragPointer = NoPointer;
        }

        private void Start()
        {
            // With a GameManager, RoundStarted positions the player (and a continued run
            // restores its saved position). Start can run a frame later than that, so only
            // snap here when testing without a GameManager.
            if (!GameManager.Instance) SnapToLane(Lanes ? Lanes.CenterLane : 0);
        }

        private void Update()
        {
            if (!Lanes) return;

            // Only accept input while a round is running. If there is no GameManager
            // in the scene (isolated testing), movement is always allowed.
            bool canControl = !GameManager.Instance || GameManager.Instance.IsPlaying;
            if (!canControl)
            {
                _dragPointer = NoPointer;
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
            _dragPointer = NoPointer;
            SetX(Lanes.GetLaneX(lane));
        }

        public void SaveRunState(RunSaveData data) => data.playerX = transform.position.x;

        public void LoadRunState(RunSaveData data)
        {
            if (!Lanes) return;
            _dragPointer = NoPointer;
            SetX(data.playerX);
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

        /// <summary>World-space horizontal distance the steering finger (or mouse) moved this frame.</summary>
        private float ReadDragDelta()
        {
            CollectPointers();

            // Keep following the finger that started the drag, as long as it is down.
            if (_dragPointer != NoPointer)
            {
                int index = _pointers.FindIndex(p => p.Id == _dragPointer);
                if (index >= 0)
                {
                    float pointerX = ScreenToWorldX(_pointers[index].Position);
                    float delta = pointerX - _lastPointerWorldX;
                    _lastPointerWorldX = pointerX;
                    return delta;
                }
                _dragPointer = NoPointer;
            }

            // A new drag can only start with a finger that just went down outside any button.
            foreach (PointerSample pointer in _pointers)
            {
                if (!pointer.Began || IsOverButton(pointer.Position)) continue;
                _dragPointer = pointer.Id;
                _lastPointerWorldX = ScreenToWorldX(pointer.Position); // Anchor only; no move this frame.
                break;
            }
            return 0f;
        }

        /// <summary>All fingers (or the mouse) currently pressed.</summary>
        private void CollectPointers()
        {
            _pointers.Clear();
#if ENABLE_INPUT_SYSTEM
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    _pointers.Add(new PointerSample
                    {
                        Id = touch.touchId.ReadValue(),
                        Position = touch.position.ReadValue(),
                        Began = touch.press.wasPressedThisFrame,
                    });
                }
                if (_pointers.Count > 0) return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                _pointers.Add(new PointerSample
                {
                    Id = MousePointer,
                    Position = mouse.position.ReadValue(),
                    Began = mouse.leftButton.wasPressedThisFrame,
                });
            }
#else
            if (Input.touchCount > 0)
            {
                // Real touches. (Touch 0 is also mirrored to the mouse; ignore that copy.)
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
                    _pointers.Add(new PointerSample
                    {
                        Id = touch.fingerId,
                        Position = touch.position,
                        Began = touch.phase == TouchPhase.Began,
                    });
                }
                return;
            }

            if (Input.GetMouseButton(0))
            {
                _pointers.Add(new PointerSample
                {
                    Id = MousePointer,
                    Position = Input.mousePosition,
                    Began = Input.GetMouseButtonDown(0),
                });
            }
#endif
        }

        /// <summary>True if the screen point is over a UI element that handles presses (buttons, FIRE).</summary>
        private bool IsOverButton(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (!eventSystem) return false;

            // Only runs on the frame a finger goes down, so a fresh event object is fine.
            var uiPointer = new PointerEventData(eventSystem) { position = screenPosition };

            _uiHits.Clear();
            eventSystem.RaycastAll(uiPointer, _uiHits);
            foreach (RaycastResult hit in _uiHits)
            {
                if (ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit.gameObject) != null ||
                    ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) != null)
                    return true;
            }
            return false;
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
