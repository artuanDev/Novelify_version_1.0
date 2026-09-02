using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Novelify
{
    public class NovelManager : MonoBehaviour
    {
        public RuntimeNovelGraph RuntimeGraph;

        [Header("UI Components")]
        public GameObject DialoguePanel;
        public GameObject BackgroundChoicesPanel;
        public TextMeshProUGUI SpeakerNameText;
        public Image SpeakerPortraitImage;
        public TextMeshProUGUI DialogueText;

        [Header("Choice Button UI")]
        public Button ChoiceButtonPrefab;
        public Transform ChoiceButtonContainer;

        private Dictionary<string, RuntimeDialogueNode> _nodeLookup = new Dictionary<string, RuntimeDialogueNode>();
        private RuntimeDialogueNode _currentNode;

        private void Start()
        {
            foreach (var node in RuntimeGraph.AllNodes)
            {
                _nodeLookup[node.NodeID] = node;
            }  

            if(!string.IsNullOrEmpty(RuntimeGraph.EntryNodeID))
            {
                ShowNode(RuntimeGraph.EntryNodeID);
            }
            else
            {
                EndDialogue();
            }
        }

        private void Update()
        {
            if(Mouse.current.leftButton.wasPressedThisFrame && _currentNode != null && _currentNode.Choices.Count == 0)
            {
                if (!string.IsNullOrEmpty(_currentNode.NextNodeID))
                {
                    ShowNode(_currentNode.NextNodeID);
                }
                else
                {
                    EndDialogue();
                }
            }
        }

        private void ShowNode(string nodeID)
        {
            if(!_nodeLookup.ContainsKey(nodeID))
            {
                EndDialogue();
                return;
            }

            _currentNode = _nodeLookup[nodeID];
            DialoguePanel.SetActive(true);
            SpeakerNameText.SetText(_currentNode.SpeakerName);
            UpdateSpeakerPortrait(_currentNode.SpeakerPortrait);
            DialogueText.SetText(_currentNode.DialogueText);
            BackgroundChoicesPanel.SetActive(false);

            foreach (Transform child in ChoiceButtonContainer)
            {
                Destroy(child.gameObject);
            }

            if(_currentNode.Choices.Count > 0)
            {
                BackgroundChoicesPanel.SetActive(true);
                foreach(var choice in _currentNode.Choices)
                {
                    Button button = Instantiate(ChoiceButtonPrefab, ChoiceButtonContainer);

                    TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                    if(buttonText != null)
                    {
                        buttonText.text = choice.ChoiceText;
                    }

                    if(button != null)
                    {
                        button.onClick.AddListener(() =>
                        {
                            if (!string.IsNullOrEmpty(choice.DestinationNodeID))
                            {
                                ShowNode(choice.DestinationNodeID);
                            }
                            else
                            {
                                EndDialogue();
                            }
                        });
                    }
                }
            }
        }

        private void EndDialogue()
        {
            DialoguePanel.SetActive(false);
            _currentNode = null;

            UpdateSpeakerPortrait(null);

            foreach (Transform child in ChoiceButtonContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void UpdateSpeakerPortrait(Sprite portrait)
        {
            if(SpeakerPortraitImage == null)
            {
                return;
            }

            SpeakerPortraitImage.sprite = portrait;
            SpeakerPortraitImage.enabled = portrait != null;
        }
    }
}
