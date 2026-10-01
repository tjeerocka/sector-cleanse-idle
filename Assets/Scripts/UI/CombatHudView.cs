using System.Text;
using SectorCleanse.Combat;
using SectorCleanse.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// Wave/elite part of the HUD:
    ///  * centre banner for WaveDirector announcements (fades out);
    ///  * elite countdown bar (only while an elite is up);
    ///  * list of active buffs/debuffs with their seconds left, plus shield charges.
    /// </summary>
    public class CombatHudView : MonoBehaviour
    {
        [SerializeField] private WaveDirector director;
        [SerializeField] private RunEffects effects;
        [SerializeField] private PlayerSquad squad;

        [Header("Banner")]
        [SerializeField] private Text bannerLabel;
        [SerializeField, Min(0.1f)] private float bannerDuration = 2.5f;

        [Header("Elite timer")]
        [SerializeField] private GameObject eliteBar;
        [SerializeField] private RectTransform eliteBarFill;
        [SerializeField] private Text eliteBarLabel;

        [Header("Effects")]
        [SerializeField] private Text effectsLabel;

        private readonly StringBuilder _builder = new StringBuilder();
        private float _bannerTimer;
        private Color _bannerColor = Color.white;
        private string _shownEffects;

        private void Awake()
        {
            if (!director) director = FindAnyObjectByType<WaveDirector>();
            if (!effects) effects = FindAnyObjectByType<RunEffects>();
            if (!squad) squad = FindAnyObjectByType<PlayerSquad>();
        }

        private void OnEnable()
        {
            if (director) director.Announced += ShowBanner;
            if (bannerLabel) bannerLabel.text = "";
            _bannerTimer = 0f;
        }

        private void OnDisable()
        {
            if (director) director.Announced -= ShowBanner;
        }

        private void ShowBanner(string text, Color color)
        {
            if (!bannerLabel) return;
            bannerLabel.text = text;
            _bannerColor = color;
            bannerLabel.color = color;
            _bannerTimer = bannerDuration;
        }

        private void Update()
        {
            UpdateBanner();
            UpdateEliteBar();
            UpdateEffects();
        }

        private void UpdateBanner()
        {
            if (!bannerLabel || _bannerTimer <= 0f) return;
            _bannerTimer -= Time.unscaledDeltaTime;
            if (_bannerTimer <= 0f)
            {
                bannerLabel.text = "";
                return;
            }
            Color c = _bannerColor;
            c.a = Mathf.Clamp01(_bannerTimer / 0.5f); // Fade out over the last half second.
            bannerLabel.color = c;
        }

        private void UpdateEliteBar()
        {
            if (!eliteBar) return;
            Elite elite = director ? director.CurrentElite : null;
            bool show = elite && !elite.IsDead;
            if (eliteBar.activeSelf != show) eliteBar.SetActive(show);
            if (!show) return;

            bool marching = elite.State == Elite.Phase.Marching;
            if (eliteBarFill) eliteBarFill.localScale = new Vector3(elite.TimeLeftFraction, 1f, 1f);
            if (eliteBarLabel)
            {
                string who = elite.IsBoss ? "BOSS" : "ELITE";
                eliteBarLabel.text = marching
                    ? $"{who} BREAKING THROUGH!"
                    : $"KILL THE {who}: {elite.TimeLeft:0.0}s";
            }
        }

        private void UpdateEffects()
        {
            if (!effectsLabel) return;

            _builder.Clear();
            if (effects)
            {
                foreach (RunEffects.ActiveEffect active in effects.Active)
                {
                    EffectInfo info = EffectInfo.Of(active.Type);
                    _builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(info.Color)).Append('>')
                        .Append(info.Name).Append(' ').Append(Mathf.CeilToInt(active.Remaining)).Append("s</color>\n");
                }
            }
            if (squad && squad.ShieldCharges > 0)
                _builder.Append("<color=#66CCFF>SHIELD x").Append(squad.ShieldCharges).Append("</color>\n");

            string text = _builder.ToString();
            if (text == _shownEffects) return;
            _shownEffects = text;
            effectsLabel.text = text;
        }
    }
}
