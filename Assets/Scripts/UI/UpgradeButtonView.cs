using SectorCleanse.Core;
using SectorCleanse.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace SectorCleanse.UI
{
    /// <summary>
    /// One shop row: shows name, owned level and price for an upgrade, buys a level
    /// on click, and greys out when unaffordable or maxed.
    /// Reusable for every upgrade, only <see cref="upgradeId"/> changes.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UpgradeButtonView : MonoBehaviour
    {
        [SerializeField] private UpgradeShop shop;
        [SerializeField] private string upgradeId = UpgradeShop.DamageId;
        [SerializeField] private Text label;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (!shop) shop = FindAnyObjectByType<UpgradeShop>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(Buy);
            if (shop) shop.UpgradesChanged += Refresh;
            if (GameManager.Instance) GameManager.Instance.BankedMoneyChanged += OnBankChanged;
            Refresh();
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(Buy);
            if (shop) shop.UpgradesChanged -= Refresh;
            if (GameManager.Instance) GameManager.Instance.BankedMoneyChanged -= OnBankChanged;
        }

        private void Buy()
        {
            if (shop) shop.TryBuy(upgradeId);
        }

        private void OnBankChanged(int _) => Refresh();

        private void Refresh()
        {
            if (!shop || !label) return;

            UpgradeShop.Upgrade upgrade = shop.Find(upgradeId);
            if (upgrade == null)
            {
                label.text = $"Unknown upgrade '{upgradeId}'";
                _button.interactable = false;
                return;
            }

            bool maxed = shop.IsMaxed(upgradeId);
            string price = maxed ? "MAX" : $"${shop.GetCost(upgradeId)}";
            string owned = upgrade.maxLevel > 0
                ? $"{shop.GetLevel(upgradeId)}/{upgrade.maxLevel}"
                : shop.GetLevel(upgradeId).ToString();
            label.text = $"{upgrade.displayName}   {price}\n{upgrade.description}   OWNED {owned}";
            _button.interactable = !maxed && shop.CanAfford(upgradeId);
        }
    }
}
