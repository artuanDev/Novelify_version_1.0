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
        public Image Speaker_Body;
        public Image Speaker_Eyes;
        public Image Speaker_Details;
        public Image Speaker_Mouth;
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
            UpdateSpeakerPortrait(
                _currentNode.PortraitBody,
                _currentNode.PortraitEyes,
                _currentNode.PortraitDetails,
                _currentNode.PortraitMouth
                );

            //DialogueText.SetText(_currentNode.DialogueText); //This shows the text inmediately
            StartCoroutine(NovelifyUtilities.ShowTextLetterByLetter(_currentNode.DialogueText, DialogueText));
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

            UpdateSpeakerPortrait(null, null, null, null);

            foreach (Transform child in ChoiceButtonContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void UpdateSpeakerPortrait(
            Sprite portrait_body, Sprite portrait_eyes, Sprite portrait_details, Sprite portrait_mouth)
        {
            if(Speaker_Body == null)
            {
                return;
            }

            Speaker_Body.sprite = portrait_body;
            Speaker_Eyes.sprite = portrait_eyes;
            Speaker_Details.sprite = portrait_details;
            Speaker_Mouth.sprite = portrait_mouth;

            Speaker_Body.enabled = portrait_body != null;
            Speaker_Eyes.enabled = portrait_eyes != null;
            Speaker_Details.enabled = portrait_details != null;
            Speaker_Mouth.enabled = portrait_mouth != null;
        }
    }
}
