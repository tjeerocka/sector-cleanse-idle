using System.Collections.Generic;
using UnityEngine;

namespace SectorCleanse.Player
{
    /// <summary>
    /// Shows the squad: one orange square per soldier (the player itself is the
    /// green square this component sits on), arranged in rows just below the player
    /// so they move with it.
    ///
    /// Listens to <see cref="PlayerSquad.SoldierCountChanged"/>; soldier objects are
    /// pooled and re-laid out (last row centred) whenever the count changes.
    /// <see cref="ActiveSoldiers"/> is used by the Weapon to fire one bullet per soldier.
    /// </summary>
    [RequireComponent(typeof(PlayerSquad))]
    [DisallowMultipleComponent]
    public class SquadFormation : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private Sprite soldierSprite;
        [SerializeField] private Color soldierColor = new Color(1f, 0.55f, 0.1f);

        [Tooltip("World-space size of a soldier square.")]
        [SerializeField, Min(0.05f)] private float soldierSize = 0.25f;

        [Header("Layout (world units)")]
        [Tooltip("Soldiers per row. Keep columns × spacing.x under the lane width so all bullets stay in-lane.")]
        [SerializeField, Min(1)] private int columns = 4;
        [SerializeField] private Vector2 spacing = new Vector2(0.3f, 0.32f);

        [Tooltip("Distance from the player's centre down to the first row.")]
        [SerializeField, Min(0f)] private float firstRowOffset = 0.65f;

        [Tooltip("Soldiers beyond this are still counted (health) but not drawn / don't fire.")]
        [SerializeField, Min(0)] private int maxVisible = 24;

        private readonly List<Transform> _pool = new List<Transform>();
        private readonly List<Transform> _active = new List<Transform>();
        private PlayerSquad _squad;

        /// <summary>Visible soldier transforms (excludes the player).</summary>
        public IReadOnlyList<Transform> ActiveSoldiers => _active;

        private void Awake()
        {
            _squad = GetComponent<PlayerSquad>();
        }

        private void OnEnable()
        {
            _squad.SoldierCountChanged += Refresh;
            Refresh(_squad.SoldierCount);
        }

        private void OnDisable()
        {
            _squad.SoldierCountChanged -= Refresh;
        }

        private void Refresh(int soldierCount)
        {
            // The player is one of the soldiers; only the others get orange squares.
            int visible = Mathf.Clamp(soldierCount - 1, 0, maxVisible);

            while (_pool.Count < visible) _pool.Add(CreateSoldier(_pool.Count));

            _active.Clear();
            for (int i = 0; i < _pool.Count; i++)
            {
                bool on = i < visible;
                _pool[i].gameObject.SetActive(on);
                if (!on) continue;

                _pool[i].localPosition = LocalPositionFor(i, visible);
                _active.Add(_pool[i]);
            }
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

        private Transform CreateSoldier(int index)
        {
            var go = new GameObject($"Soldier_{index}");
            go.transform.SetParent(transform, false);

            Vector3 parentScale = transform.lossyScale;
            go.transform.localScale = new Vector3(soldierSize / parentScale.x, soldierSize / parentScale.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = soldierSprite;
            sr.color = soldierColor;
            sr.sortingOrder = 9;
            return go.transform;
        }
    }
}
