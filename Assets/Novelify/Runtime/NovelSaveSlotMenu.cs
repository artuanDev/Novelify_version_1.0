using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Novelify
{
    /// <summary>Optional UI binder for save slots. Assign existing controls; no visual style is imposed.</summary>
    public sealed class NovelSaveSlotMenu : MonoBehaviour
    {
        public NovelGraphRunner Manager;
        [Tooltip("Existing slots are listed here for resume/delete actions.")]
        public TMP_Dropdown SlotDropdown;
        [Tooltip("Optional slot name for a new save. Blank uses the selected slot or 'slot_1'.")]
        public TMP_InputField SlotNameInput;
        public Button SaveButton;
        public Button LoadButton;
        public Button DeleteButton;
        public TextMeshProUGUI StatusText;

        private void OnEnable()
        {
            SaveButton?.onClick.AddListener(SaveSelected);
            LoadButton?.onClick.AddListener(LoadSelected);
            DeleteButton?.onClick.AddListener(DeleteSelected);
            if (Manager != null) Manager.SlotSaveCompleted += OnDeferredSaveCompleted;
            RefreshSlots();
        }

        private void OnDisable()
        {
            SaveButton?.onClick.RemoveListener(SaveSelected);
            LoadButton?.onClick.RemoveListener(LoadSelected);
            DeleteButton?.onClick.RemoveListener(DeleteSelected);
            if (Manager != null) Manager.SlotSaveCompleted -= OnDeferredSaveCompleted;
        }

        public void RefreshSlots()
        {
            if (SlotDropdown == null || Manager == null) return;
            string selected = SelectedSlot();
            SlotDropdown.ClearOptions();
            SlotDropdown.AddOptions(new System.Collections.Generic.List<string>(Manager.SaveStorage.ListSlots()));
            int index = SlotDropdown.options.FindIndex(option => string.Equals(option.text, selected, StringComparison.Ordinal));
            SlotDropdown.value = Mathf.Max(0, index);
            SlotDropdown.RefreshShownValue();
            bool hasSlot = SlotDropdown.options.Count > 0;
            if (LoadButton != null) LoadButton.interactable = hasSlot;
            if (DeleteButton != null) DeleteButton.interactable = hasSlot;
        }

        public void SaveSelected()
        {
            if (Manager == null) { Show("NovelGraphRunner is not assigned."); return; }
            string slot = SlotNameInput != null ? SlotNameInput.text?.Trim() : string.Empty;
            if (string.IsNullOrEmpty(slot)) slot = SelectedSlot();
            if (string.IsNullOrEmpty(slot)) slot = "slot_1";
            NovelPersistenceResult result = Manager.SaveSlot(slot);
            Show(result.Status == NovelPersistenceStatus.Pending ? "Save pending…" : result.ToString());
            if (result.Succeeded) RefreshSlots();
        }

        public void LoadSelected()
        {
            if (Manager == null) { Show("NovelGraphRunner is not assigned."); return; }
            string slot = SelectedSlot();
            if (string.IsNullOrEmpty(slot)) { Show("Select a save slot."); return; }
            NovelPersistenceResult result = Manager.LoadSlot(slot);
            Show(result.ToString());
        }

        public void DeleteSelected()
        {
            if (Manager == null) { Show("NovelGraphRunner is not assigned."); return; }
            string slot = SelectedSlot();
            if (string.IsNullOrEmpty(slot)) { Show("Select a save slot."); return; }
            NovelPersistenceResult result = Manager.SaveStorage.DeleteSlot(slot);
            Show(result.ToString());
            RefreshSlots();
        }

        private void OnDeferredSaveCompleted(string slot, NovelPersistenceResult result)
        {
            Show(result.Succeeded ? $"Saved {slot}." : result.ToString());
            if (result.Succeeded) RefreshSlots();
        }

        private string SelectedSlot()
        {
            if (SlotDropdown == null || SlotDropdown.options.Count == 0) return string.Empty;
            int index = Mathf.Clamp(SlotDropdown.value, 0, SlotDropdown.options.Count - 1);
            return SlotDropdown.options[index].text;
        }

        private void Show(string message)
        {
            if (StatusText != null) StatusText.SetText(message ?? string.Empty);
        }
    }
}
