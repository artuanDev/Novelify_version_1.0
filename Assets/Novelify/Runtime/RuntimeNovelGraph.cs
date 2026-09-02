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
        public string nodeID;
        public string SpeakerName;
        public string DialogueText;
        public string NodeID;
    }
}
