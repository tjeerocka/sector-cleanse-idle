using UnityEngine;

namespace SectorCleanse.Core
{
    /// <summary>
    /// Single source of truth for lane geometry.
    ///
    /// Lanes are vertical columns centred on this GameObject's X position. The
    /// player line (where the squad stands) sits at the bottom, and the spawn line
    /// (where enemies / buffs / debuffs appear) sits at the top. Every system that
    /// needs a lane position (player, spawner, projectiles, lane effects) should ask
    /// this component instead of hard-coding coordinates, so the layout can be
    /// tweaked in one place.
    /// </summary>
    [DisallowMultipleComponent]
    public class LaneSystem : MonoBehaviour
    {
        public static LaneSystem Instance { get; private set; }

        [Header("Layout")]
        [Tooltip("Number of lanes. The game design uses 3, but nothing below assumes it.")]
        [SerializeField, Min(1)] private int laneCount = 3;

        [Tooltip("Horizontal distance (world units) between lane centres.")]
        [SerializeField, Min(0.1f)] private float laneSpacing = 2f;

        [Tooltip("Y offset (from this transform) of the line the squad stands on.")]
        [SerializeField] private float playerLineY = -4f;

        [Tooltip("Y offset (from this transform) where lane objects spawn.")]
        [SerializeField] private float spawnLineY = 6f;

        public int LaneCount => laneCount;
        public int CenterLane => laneCount / 2;
        public float LaneSpacing => laneSpacing;
        public float PlayerLineWorldY => transform.position.y + playerLineY;
        public float SpawnLineWorldY => transform.position.y + spawnLineY;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Duplicate {nameof(LaneSystem)} on '{name}' destroyed.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------

        public bool IsValidLane(int lane) => lane >= 0 && lane < laneCount;

        public int ClampLane(int lane) => Mathf.Clamp(lane, 0, laneCount - 1);

        /// <summary>World X of a lane's centre. Lane 0 is the leftmost.</summary>
        public float GetLaneX(int lane)
        {
            float offsetFromCenter = ClampLane(lane) - (laneCount - 1) * 0.5f;
            return transform.position.x + offsetFromCenter * laneSpacing;
        }

        /// <summary>Where the squad stands when occupying <paramref name="lane"/>.</summary>
        public Vector3 GetPlayerPosition(int lane) =>
            new Vector3(GetLaneX(lane), PlayerLineWorldY, transform.position.z);

        /// <summary>Where a lane object (enemy, buff, debuff) spawns in <paramref name="lane"/>.</summary>
        public Vector3 GetSpawnPosition(int lane) =>
            new Vector3(GetLaneX(lane), SpawnLineWorldY, transform.position.z);

        /// <summary>Closest lane to a world X coordinate (used for tap-to-move).</summary>
        public int GetNearestLane(float worldX)
        {
            float laneFloat = (worldX - transform.position.x) / laneSpacing + (laneCount - 1) * 0.5f;
            return ClampLane(Mathf.RoundToInt(laneFloat));
        }

        // ------------------------------------------------------------------
        // Editor visualisation (graybox helper)
        // ------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            float bottom = transform.position.y + playerLineY;
            float top = transform.position.y + spawnLineY;
            float halfWidth = laneCount * laneSpacing * 0.5f;
            float z = transform.position.z;

            // Lane centres.
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            for (int i = 0; i < laneCount; i++)
            {
                float x = GetLaneX(i);
                Gizmos.DrawLine(new Vector3(x, bottom, z), new Vector3(x, top, z));
            }

            // Player line (green) and spawn line (red).
            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(transform.position.x - halfWidth, bottom, z),
                            new Vector3(transform.position.x + halfWidth, bottom, z));
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(transform.position.x - halfWidth, top, z),
                            new Vector3(transform.position.x + halfWidth, top, z));
        }
    }
}
