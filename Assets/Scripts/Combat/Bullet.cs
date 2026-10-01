using SectorCleanse.Core;
using UnityEngine;

namespace SectorCleanse.Combat
{
    /// <summary>
    /// Flies straight up and damages the first enemy it overlaps. Bullets fired while
    /// the FIRE button is held are "manual": they pay more and can hurt elites.
    /// Uses a swept overlap test against <see cref="Enemy.Active"/> so fast bullets
    /// can't skip over an enemy on a low-framerate frame.
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        private double _damage;
        private float _speed;
        private float _maxY;
        private bool _manual;

        public void Initialize(double damage, float speed, float maxY, bool manual)
        {
            _damage = damage;
            _manual = manual;
            _speed = speed;
            _maxY = maxY;
        }

        private void Update()
        {
            if (GameManager.Instance && !GameManager.Instance.IsPlaying) return; // Freeze on game over.

            float previousY = transform.position.y;
            transform.position += Vector3.up * (_speed * Time.deltaTime);

            if (TryHit(previousY) || transform.position.y > _maxY) Destroy(gameObject);
        }

        private bool TryHit(float previousY)
        {
            Vector3 p = transform.position;
            float halfWidth = 0.5f * transform.lossyScale.x;
            float halfHeight = 0.5f * transform.lossyScale.y;

            // Vertical span covered by the bullet this frame.
            float sweepBottom = previousY - halfHeight;
            float sweepTop = p.y + halfHeight;

            var enemies = Enemy.Active;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy.IsDead) continue;

                Vector3 e = enemy.transform.position;
                bool overlapX = Mathf.Abs(p.x - e.x) <= halfWidth + enemy.HalfWidth;
                bool overlapY = e.y + enemy.HalfHeight >= sweepBottom && e.y - enemy.HalfHeight <= sweepTop;
                if (!overlapX || !overlapY) continue;

                enemy.TakeHit(_damage, _manual); // Absorbed even if the enemy is immune.
                return true;
            }
            return false;
        }
    }
}
