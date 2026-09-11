using TMPro;
using UnityEngine;

namespace Novelify.Samples.TopDownKeyQuest
{
    /// <summary>Reflects Novelify state in ordinary game UI.</summary>
    public sealed class TopDownQuestHUD : MonoBehaviour
    {
        public NovelGraphRunner NovelifyRunner;
        public NovelVariableDefinition HasKey;
        public TMP_Text ObjectiveText;
        public TMP_Text KeyText;

        private bool _doorOpen;

        private void OnEnable()
        {
            if (NovelifyRunner == null) return;
            NovelifyRunner.Session.State.ValueChanged += OnValueChanged;
            NovelifyRunner.Session.State.StateReset += Refresh;
            NovelifyRunner.Session.EventRaised += OnNovelifyEvent;
            Refresh();
        }

        private void OnDisable()
        {
            if (NovelifyRunner == null) return;
            NovelifyRunner.Session.State.ValueChanged -= OnValueChanged;
            NovelifyRunner.Session.State.StateReset -= Refresh;
            NovelifyRunner.Session.EventRaised -= OnNovelifyEvent;
        }

        private void OnValueChanged(NovelVariableDefinition variable, RuntimeValue value)
        {
            if (variable == HasKey) Refresh();
        }

        private void OnNovelifyEvent(string eventName)
        {
            if (eventName != TopDownNovelDoor.OpenEvent) return;
            _doorOpen = true;
            Refresh();
        }

        private void Refresh()
        {
            bool hasKey = NovelifyRunner != null && HasKey != null && NovelifyRunner.Session.GetBool(HasKey);
            if (KeyText != null) KeyText.text = hasKey ? "KEY  ACQUIRED" : "KEY  NOT FOUND";
            if (ObjectiveText == null) return;
            ObjectiveText.text = _doorOpen
                ? "OBJECTIVE  Complete — the north door is open."
                : hasKey
                    ? "OBJECTIVE  Return to the north door."
                    : "OBJECTIVE  Talk to Daisy and convince her to share the brass key.";
        }
    }
}
