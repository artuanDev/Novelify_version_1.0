using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    public class RuntimeNovelGraph : ScriptableObject
    {
        public string EntryNodeID;
        //  This will hold all nodes in the graph we want to load.
        public List<RuntimeDialogueNode> AllNodes = new List<RuntimeDialogueNode>();
    }
    [Serializable]
    public class RuntimeDialogueNode
    {
        public string NodeID;
        public string SpeakerName;
        public Sprite PortraitBody;
        public Sprite PortraitDetails;
        public Sprite PortraitEyes;
        public Sprite PortraitMouth;
        public string DialogueText;
        public List<ChoiceData> Choices = new List<ChoiceData>();
        public string NextNodeID;
    }

    [Serializable]
    public class ChoiceData
    {
        public string ChoiceText;
        public string DestinationNodeID;
    }

}
