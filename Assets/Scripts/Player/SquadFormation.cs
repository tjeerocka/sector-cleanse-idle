using System.Collections.Generic;
using UnityEngine;

namespace SectorCleanse.Player
{
    /// <summary>
    /// Shows the deployed soldiers below the green player: one square per living
    /// soldier, labelled with its tier and tinted by tier (T0 orange, higher tiers
    /// shift towards red / magenta / purple). Soldiers move with the player.
    ///
    /// Rebuilt from <see cref="PlayerSquad.Soldiers"/> whenever the squad changes.
    /// <see cref="Units"/> is used by the Weapon to fire one bullet per soldier.
    /// </summary>
    [RequireComponent(typeof(PlayerSquad))]
    [DisallowMultipleComponent]
    public class SquadFormation : MonoBehaviour
    {
        public struct Unit
        {
            public Transform Transform;
            public int Tier;
        }

        [Header("Look")]
        [SerializeField] private Sprite soldierSprite;
        [SerializeField] private Color soldierColor = new Color(1f, 0.55f, 0.1f);

        [Tooltip("World-space size of a soldier square.")]
        [SerializeField, Min(0.05f)] private float soldierSize = 0.32f;

        [Header("Layout (world units)")]
        [SerializeField, Min(1)] private int columns = 5;
        [SerializeField] private Vector2 spacing = new Vector2(0.36f, 0.38f);

        [Tooltip("Distance from the player's centre down to the first row.")]
        [SerializeField, Min(0f)] private float firstRowOffset = 0.7f;

        private static Font _labelFont;

        private readonly List<Transform> _pool = new List<Transform>();
        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private readonly List<TextMesh> _labels = new List<TextMesh>();
        private readonly List<Unit> _units = new List<Unit>();
        private PlayerSquad _squad;

        /// <summary>Visible soldiers (excludes the player) with their tiers.</summary>
        public IReadOnlyList<Unit> Units => _units;

        private void Awake()
        {
            _squad = GetComponent<PlayerSquad>();
        }

        private void OnEnable()
        {
            _squad.SquadChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            _squad.SquadChanged -= Refresh;
        }

        private void Refresh()
        {
            var soldiers = _squad.Soldiers;
            int count = soldiers.Count;

            while (_pool.Count < count) CreateSoldier(_pool.Count);

            _units.Clear();
            for (int i = 0; i < _pool.Count; i++)
            {
                bool on = i < count;
                _pool[i].gameObject.SetActive(on);
                if (!on) continue;

                int tier = soldiers[i].Tier;
                _pool[i].localPosition = LocalPositionFor(i, count);
                _renderers[i].color = ColorForTier(tier);
                _labels[i].text = tier.ToString();
                _units.Add(new Unit { Transform = _pool[i], Tier = tier });
            }
        }

        /// <summary>T0 = base orange; each tier rotates the hue a little towards red → magenta → purple.</summary>
        private Color ColorForTier(int tier)
        {
            Color.RGBToHSV(soldierColor, out float h, out float s, out float v);
            h = Mathf.Repeat(h - tier * 0.035f, 1f);
            return Color.HSVToRGB(h, s, v);
        }

        private Vector3 LocalPositionFor(int index, int total)
        {
            int row = index / columns;
            int col = index % columns;
            int inThisRow = Mathf.Min(columns, total - row * columns); // centre a partial last row

            Vector2 world = new Vector2(
                (col - (inThisRow - 1) * 0.5f) * spacing.x,
                -(firstRowOffset + row * spacing.y));

            // Convert to local space so the player's own scale doesn't distort the formation.
            Vector3 parentScale = transform.lossyScale;
            return new Vector3(world.x / parentScale.x, world.y / parentScale.y, 0f);
        }

        private void CreateSoldier(int index)
        {
            var go = new GameObject($"Soldier_{index}");
            go.transform.SetParent(transform, false);

            Vector3 parentScale = transform.lossyScale;
            go.transform.localScale = new Vector3(soldierSize / parentScale.x, soldierSize / parentScale.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = soldierSprite;
            sr.color = soldierColor;
            sr.sortingOrder = 9;

            // Tier number on the square.
            if (!_labelFont) _labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelGo = new GameObject("Tier");
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<TextMesh>();
            label.font = _labelFont;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.12f;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            var labelRenderer = labelGo.GetComponent<MeshRenderer>();
            labelRenderer.sharedMaterial = _labelFont.material;
            labelRenderer.sortingOrder = 21;

            _pool.Add(go.transform);
            _renderers.Add(sr);
            _labels.Add(label);
        }
    }
}
