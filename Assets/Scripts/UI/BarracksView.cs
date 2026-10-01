using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// Barracks screen (opened from the start menu):
    ///  * header with front-line count;
    ///  * RECRUIT button (buy a T0 soldier with banked money);
    ///  * one row per owned tier, strongest first:
    ///      "T3  DMG/HP 125  |  RESERVE 2/5  FRONT 4"  [DEPLOY] [RETURN] [MERGE]
    ///
    /// Rows are built at runtime into <see cref="rowContainer"/> (a vertical layout
    /// inside a scroll view) and rebuilt whenever the barracks or bank changes.
    /// </summary>
    public class BarracksView : MonoBehaviour
    {
        [SerializeField] private Barracks barracks;
        [SerializeField] private Text headerLabel;
        [SerializeField] private Text infoLabel;
        [SerializeField] private Button recruitButton;
        [SerializeField] private Text recruitLabel;
        [SerializeField] private RectTransform rowContainer;

        [Header("Row look")]
        [SerializeField, Min(40f)] private float rowHeight = 120f;
        [SerializeField] private Color deployColor = new Color(0.2f, 0.7f, 0.35f);
        [SerializeField] private Color returnColor = new Color(0.45f, 0.45f, 0.5f);
        [SerializeField] private Color mergeColor = new Color(0.65f, 0.35f, 0.9f);

        private static Font _font;

        private void Awake()
        {
            if (!barracks) barracks = FindAnyObjectByType<Barracks>();
            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void OnEnable()
        {
            if (barracks) barracks.Changed += Rebuild;
            if (GameManager.Instance) GameManager.Instance.BankedMoneyChanged += OnBankChanged;
            if (recruitButton) recruitButton.onClick.AddListener(Recruit);
            Rebuild();
        }

        private void OnDisable()
        {
            if (barracks) barracks.Changed -= Rebuild;
            if (GameManager.Instance) GameManager.Instance.BankedMoneyChanged -= OnBankChanged;
            if (recruitButton) recruitButton.onClick.RemoveListener(Recruit);
        }

        private void OnBankChanged(int _) => Rebuild();

        private void Recruit()
        {
            if (barracks) barracks.Recruit();
        }

        // ------------------------------------------------------------------
        // Building the screen
        // ------------------------------------------------------------------

        private void Rebuild()
        {
            if (!barracks) return;

            if (headerLabel)
                headerLabel.text = $"BARRACKS   FRONT LINE {barracks.DeployedTotal}/{Barracks.FrontLineCap}";

            if (infoLabel)
            {
                infoLabel.text = barracks.FrontLineLocked
                    ? "FRONT LINE LOCKED: A RUN IS IN PROGRESS. CONTINUE OR ABANDON IT FIRST."
                    : $"{Barracks.ReserveCap} IN RESERVE MERGE INTO 1 OF THE NEXT TIER (x{Barracks.ReserveCap} DMG/HP).";
            }

            if (recruitButton)
            {
                bool reserveFull = barracks.Reserve(0) >= Barracks.ReserveCap;
                recruitButton.interactable = barracks.CanRecruit;
                if (recruitLabel)
                {
                    recruitLabel.text = reserveFull
                        ? "T0 RESERVE FULL - MERGE OR DEPLOY"
                        : $"RECRUIT T0 SOLDIER   ${barracks.RecruitCost}";
                }
            }

            if (!rowContainer) return;

            for (int i = rowContainer.childCount - 1; i >= 0; i--)
                Destroy(rowContainer.GetChild(i).gameObject);

            for (int tier = Mathf.Max(0, barracks.HighestOwnedTier); tier >= 0; tier--)
            {
                if (tier > 0 && barracks.Owned(tier) == 0) continue;
                CreateRow(tier);
            }
        }

        private void CreateRow(int tier)
        {
            var row = new GameObject($"Tier_{tier}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(rowContainer, false);
            row.GetComponent<Image>().color = new Color(1f, 1f, 1f, tier % 2 == 0 ? 0.05f : 0.09f);
            var layout = row.GetComponent<LayoutElement>();
            layout.preferredHeight = rowHeight;
            layout.minHeight = rowHeight;

            string power = NumberFormat.Short(Barracks.PowerOfTier(tier));
            CreateText(row.transform,
                $"T{tier}   DMG/HP {power}\nRESERVE {barracks.Reserve(tier)}/{Barracks.ReserveCap}   FRONT {barracks.Deployed(tier)}",
                34, 0.02f, 0.5f);

            CreateButton(row.transform, "DEPLOY", deployColor, 0.51f, 0.66f,
                barracks.CanDeploy(tier), () => barracks.Deploy(tier));
            CreateButton(row.transform, "RETURN", returnColor, 0.67f, 0.82f,
                barracks.CanReturn(tier), () => barracks.Return(tier));
            CreateButton(row.transform, "MERGE", mergeColor, 0.83f, 0.98f,
                barracks.CanMerge(tier), () => barracks.Merge(tier));
        }

        private static Text CreateText(Transform parent, string content, int size, float xMin, float xMax)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMax, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private static void CreateButton(Transform parent, string label, Color color, float xMin, float xMax,
            bool interactable, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(xMin, 0.15f);
            rt.anchorMax = new Vector2(xMax, 0.85f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            go.GetComponent<Image>().color = color;
            var button = go.GetComponent<Button>();
            button.interactable = interactable;
            button.onClick.AddListener(onClick);

            Text text = CreateText(go.transform, label, 30, 0f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
        }
    }
}
