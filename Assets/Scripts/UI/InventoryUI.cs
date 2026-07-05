using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace ZulfarakRPG
{
    public class InventoryUI : MonoBehaviour
    {
        [Header("Layout")]
        public Transform bagGrid;
        public Transform equipmentPanel;
        public GameObject itemSlotPrefab;

        [Header("Equipment Slots (assign in Inspector)")]
        public Image weaponSlot;
        public Image helmetSlot;
        public Image chestSlot;
        public Image legsSlot;
        public Image bootsSlot;
        public Image glovesSlot;
        public Image ringSlot;
        public Image amuletSlot;

        [Header("Tooltip")]
        public GameObject tooltip;
        public TextMeshProUGUI tooltipName;
        public TextMeshProUGUI tooltipRarity;
        public TextMeshProUGUI tooltipStats;
        public TextMeshProUGUI tooltipDesc;

        [Header("Stats Summary")]
        public TextMeshProUGUI totalHpText;
        public TextMeshProUGUI totalAtkText;
        public TextMeshProUGUI totalDefText;

        private void OnEnable()
        {
            Inventory.Instance.OnInventoryChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (Inventory.Instance != null)
                Inventory.Instance.OnInventoryChanged -= Refresh;
        }

        private void Refresh()
        {
            RefreshBag();
            RefreshEquipment();
            RefreshStats();
            HideTooltip();
        }

        private void RefreshBag()
        {
            foreach (Transform child in bagGrid) Destroy(child.gameObject);

            foreach (var invItem in Inventory.Instance.Items)
            {
                var data = ItemDatabase.Instance.Get(invItem.itemId);
                if (data == null) continue;

                var slot = Instantiate(itemSlotPrefab, bagGrid);
                SetupSlot(slot, data, invItem.quantity, () => OnBagItemClicked(data));
            }
        }

        private void RefreshEquipment()
        {
            var eq = Inventory.Instance.Equipment;
            SetEquipSlot(weaponSlot, eq.weaponId,  ItemType.Weapon);
            SetEquipSlot(helmetSlot, eq.helmetId,  ItemType.Helmet);
            SetEquipSlot(chestSlot,  eq.chestId,   ItemType.Chest);
            SetEquipSlot(legsSlot,   eq.legsId,    ItemType.Legs);
            SetEquipSlot(bootsSlot,  eq.bootsId,   ItemType.Boots);
            SetEquipSlot(glovesSlot, eq.glovesId,  ItemType.Gloves);
            SetEquipSlot(ringSlot,   eq.ringId,    ItemType.Ring);
            SetEquipSlot(amuletSlot, eq.amuletId,  ItemType.Amulet);
        }

        private void SetEquipSlot(Image slotImg, string itemId, ItemType slotType)
        {
            if (slotImg == null) return;

            var btn = slotImg.GetComponent<Button>();
            if (btn == null) btn = slotImg.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();

            if (string.IsNullOrEmpty(itemId))
            {
                slotImg.color = new Color(0.2f, 0.15f, 0.08f);
                slotImg.sprite = null;
                btn.onClick.AddListener(() => { /* empty slot â€” do nothing */ });
                return;
            }

            var data = ItemDatabase.Instance.Get(itemId);
            if (data == null) return;
            slotImg.color  = data.RarityColor * 0.5f + Color.black * 0.5f;
            slotImg.sprite = data.icon;
            btn.onClick.AddListener(() =>
            {
                ShowTooltip(data);
                ShowUnequipOption(data, slotType);
            });
        }

        private void SetupSlot(GameObject slot, ItemData data, int qty, System.Action onClick)
        {
            var img = slot.GetComponentInChildren<Image>();
            if (img != null) { img.sprite = data.icon; img.color = data.RarityColor; }

            var qtylbl = slot.transform.Find("Quantity")?.GetComponent<TextMeshProUGUI>();
            if (qtylbl != null) qtylbl.text = qty > 1 ? qty.ToString() : "";

            var btn = slot.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    ShowTooltip(data);
                    onClick?.Invoke();
                });
            }
        }

        private void OnBagItemClicked(ItemData data)
        {
            if (data.itemType == ItemType.Consumable)
                UseConsumable(data);
            else
                ShowEquipOption(data);
        }

        private void ShowEquipOption(ItemData data)
        {
            // Reuse tooltip panel â€” add equip button dynamically
            var equipBtn = tooltip.transform.Find("EquipButton");
            if (equipBtn == null) return;
            equipBtn.gameObject.SetActive(true);

            var btn = equipBtn.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                Inventory.Instance.Equip(data.itemId);
                HideTooltip();
            });
        }

        private void ShowUnequipOption(ItemData data, ItemType slot)
        {
            ShowTooltip(data);
            var equipBtn = tooltip.transform.Find("EquipButton");
            if (equipBtn == null) return;
            var lbl = equipBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl) lbl.text = "Desequipar";
            equipBtn.gameObject.SetActive(true);

            var btn = equipBtn.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                Inventory.Instance.Unequip(slot);
                HideTooltip();
            });
        }

        private void UseConsumable(ItemData data)
        {
            Inventory.Instance.UseConsumable(data.itemId);
            FindAnyObjectByType<CityUI>()?.RefreshPlayerInfo();
        }

        private void ShowTooltip(ItemData data)
        {
            tooltip.SetActive(true);
            tooltipName.text   = data.itemName;
            tooltipName.color  = data.RarityColor;
            tooltipRarity.text = data.rarity.ToString();
            tooltipRarity.color = data.RarityColor;
            tooltipDesc.text   = data.description;

            var statLines = new List<string>();
            if (data.bonusHp      != 0) statLines.Add($"HP +{data.bonusHp}");
            if (data.bonusAttack  != 0) statLines.Add($"ATQ +{data.bonusAttack}");
            if (data.bonusDefense != 0) statLines.Add($"DEF +{data.bonusDefense}");
            if (data.bonusSpeed   != 0) statLines.Add($"VEL +{data.bonusSpeed:0.0}");
            if (data.bonusHealPower != 0) statLines.Add($"REGEN/s +{data.bonusHealPower:0.0}");
            tooltipStats.text  = string.Join("\n", statLines);
        }

        public void HideTooltip()
        {
            if (tooltip != null) tooltip.SetActive(false);
        }

        private void RefreshStats()
        {
            var p = PlayerManager.Instance.Data;
            if (p == null) return;
            if (totalHpText)  totalHpText.text  = $"HP  {p.maxHp}";
            if (totalAtkText) totalAtkText.text = $"ATQ {p.attack}";
            if (totalDefText) totalDefText.text = $"DEF {p.defense}";
        }
    }
}
