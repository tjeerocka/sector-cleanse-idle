using System.Collections.Generic;
using SectorCleanse.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// The on-screen FIRE button: held down (by any finger) = manual fire.
    /// Also shows the gun's state: colour while firing / jammed, and a heat bar.
    /// </summary>
    public class FireButtonView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private ManualFire manualFire;
        [SerializeField] private Image background;
        [SerializeField] private Text label;

        [Header("Heat bar (fill is scaled horizontally)")]
        [SerializeField] private RectTransform heatFill;
        [SerializeField] private Image heatFillImage;

        [Header("Colours")]
        [SerializeField] private Color idleColor = new Color(0.85f, 0.3f, 0.25f, 0.85f);
        [SerializeField] private Color firingColor = new Color(1f, 0.55f, 0.2f, 1f);
        [SerializeField] private Color jammedColor = new Color(0.35f, 0.35f, 0.4f, 0.9f);
        [SerializeField] private Color heatColor = new Color(1f, 0.6f, 0.2f);
        [SerializeField] private Color overheatColor = new Color(1f, 0.25f, 0.2f);

        private readonly HashSet<int> _pressedPointers = new HashSet<int>();
        private int _shownState = -1;

        private void Awake()
        {
            if (!manualFire) manualFire = FindAnyObjectByType<ManualFire>();
            if (!background) background = GetComponent<Image>();
        }

        private void OnDisable()
        {
            _pressedPointers.Clear();
            if (manualFire) manualFire.SetButtonHeld(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressedPointers.Add(eventData.pointerId);
            if (manualFire) manualFire.SetButtonHeld(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressedPointers.Remove(eventData.pointerId);
            if (manualFire) manualFire.SetButtonHeld(_pressedPointers.Count > 0);
        }

        private void Update()
        {
            if (!manualFire) return;

            if (heatFill) heatFill.localScale = new Vector3(manualFire.Heat, 1f, 1f);
            if (heatFillImage) heatFillImage.color = manualFire.IsJammed ? overheatColor : heatColor;

            int state = manualFire.IsJammed ? 2 : manualFire.IsFiring ? 1 : 0;
            if (state == 2 && label) label.text = $"JAMMED\n{manualFire.JamRemaining:0.0}s";
            if (state == _shownState) return;
            _shownState = state;

            if (background) background.color = state == 2 ? jammedColor : state == 1 ? firingColor : idleColor;
            if (label && state != 2) label.text = state == 1 ? "FIRING\nx2" : "HOLD\nFIRE";
        }
    }
}
