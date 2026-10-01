using SectorCleanse.Core;
using SectorCleanse.Player;
using UnityEngine;

namespace SectorCleanse.Combat
{
    /// <summary>
    /// A buff dropped by a killed elite. Falls down its lane; it is caught if the
    /// player stands in that lane when it reaches the player line, otherwise missed.
    /// </summary>
    public class BuffPickup : MonoBehaviour
    {
        private static Font _font;

        public EffectType Type { get; private set; }

        private int _lane;
        private float _durationScale;
        private float _speed;
        private LaneSystem _lanes;
        private PlayerController _player;
        private WaveDirector _director;

        public void Initialize(int lane, EffectType type, float durationScale, float speed, LaneSystem lanes,
            PlayerController player, WaveDirector director)
        {
            _lane = lane;
            Type = type;
            _durationScale = durationScale;
            _speed = speed;
            _lanes = lanes;
            _player = player;
            _director = director;
            CreateLabel(EffectInfo.Of(type).Name);
        }

        private void Update()
        {
            if (!_lanes) return;
            if (GameManager.Instance && !GameManager.Instance.IsPlaying) return;

            transform.position += Vector3.down * (_speed * Time.deltaTime);
            if (transform.position.y > _lanes.PlayerLineWorldY) return;

            bool caught = _player && _player.CurrentLane == _lane;
            if (_director) _director.CollectPickup(Type, _durationScale, caught);
            Destroy(gameObject);
        }

        private void CreateLabel(string text)
        {
            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.9f, 0f);

            var label = go.AddComponent<TextMesh>();
            label.font = _font;
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.07f;
            label.fontStyle = FontStyle.Bold;
            label.color = EffectInfo.Of(Type).Color;

            var meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _font.material;
            meshRenderer.sortingOrder = 21;
        }
    }
}
