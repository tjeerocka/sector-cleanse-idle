using System.Collections.Generic;
using SectorCleanse.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// Highscores screen (opened from the start menu): ranked list of players by
    /// highest wave (then run money), the player's own line highlighted, and a
    /// "YOUR RANK" line. Rows are built at runtime into <see cref="rowContainer"/>.
    /// </summary>
    public class LeaderboardView : MonoBehaviour
    {
        [SerializeField] private PlayerProfile profile;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private Text yourRankLabel;

        [SerializeField, Min(1)] private int maxRows = 50;
        [SerializeField, Min(40f)] private float rowHeight = 80f;
        [SerializeField] private Color ownRowColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color[] podiumColors =
        {
            new Color(1f, 0.84f, 0f), new Color(0.8f, 0.8f, 0.85f), new Color(0.85f, 0.55f, 0.3f),
        };

        private static Font _font;

        private void Awake()
        {
            if (!profile) profile = FindAnyObjectByType<PlayerProfile>();
            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void OnEnable()
        {
            if (profile) profile.ScoresChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (profile) profile.ScoresChanged -= Refresh;
        }

        private void Refresh()
        {
            if (!profile) return;

            profile.Leaderboard.GetTop(maxRows, BuildRows);
            profile.GetOwnRank(rank =>
            {
                if (!yourRankLabel) return;
                yourRankLabel.text = rank > 0
                    ? $"YOUR RANK: #{rank}   ({profile.PlayerName})"
                    : "PLAY A RUN TO GET ON THE BOARD";
            });
        }

        private void BuildRows(List<LeaderboardEntry> entries)
        {
            if (!rowContainer) return;

            for (int i = rowContainer.childCount - 1; i >= 0; i--)
                Destroy(rowContainer.GetChild(i).gameObject);

            if (entries.Count == 0)
            {
                CreateRow("NO SCORES YET", Color.white, 0.03f);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                LeaderboardEntry entry = entries[i];
                bool isOwn = profile.HasName && LocalLeaderboardService.SameName(entry.name, profile.PlayerName);

                Color color = Color.white;
                if (i < podiumColors.Length) color = podiumColors[i];
                if (isOwn) color = ownRowColor;

                string you = isOwn ? "  < YOU" : "";
                CreateRow($"#{i + 1}   {entry.name}   WAVE {entry.bestWave}   BEST RUN ${entry.bestRunMoney}{you}",
                    color, isOwn ? 0.15f : (i % 2 == 0 ? 0.04f : 0.08f));
            }
        }

        private void CreateRow(string content, Color textColor, float backgroundAlpha)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(rowContainer, false);
            row.GetComponent<Image>().color = new Color(1f, 1f, 1f, backgroundAlpha);
            var layout = row.GetComponent<LayoutElement>();
            layout.preferredHeight = rowHeight;
            layout.minHeight = rowHeight;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(row.transform, false);
            var rt = (RectTransform)labelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(24f, 0f);
            rt.offsetMax = new Vector2(-24f, 0f);

            var text = labelGo.GetComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = 36;
            text.fontStyle = FontStyle.Bold;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleLeft;
        }
    }
}
