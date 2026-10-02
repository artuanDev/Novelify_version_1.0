using System;
using UnityEngine;

namespace Novelify
{
    public enum NovelCharacterAnimation { Bounce, Shake, Sway }

    [Serializable]
    public class RuntimeAnimateCharacterNode : RuntimeNode
    {
        public NovelCharacter Character;
        public string InstanceID;
        public NovelCharacterAnimation Animation;
        public float Amplitude = 25f;
        public float Frequency = 2f;
        public float Duration;
        public bool WaitForCompletion;
        [SerializeReference] public RuntimeValueExpression CharacterValue;
        [SerializeReference] public RuntimeValueExpression CharacterReferenceValue;
        [SerializeReference] public RuntimeValueExpression AmplitudeValue;
        [SerializeReference] public RuntimeValueExpression FrequencyValue;
        [SerializeReference] public RuntimeValueExpression DurationValue;
    }

    [Serializable]
    public class RuntimeStopCharacterAnimationNode : RuntimeNode
    {
        public NovelCharacter Character;
        public string InstanceID;
        [SerializeReference] public RuntimeValueExpression CharacterValue;
        [SerializeReference] public RuntimeValueExpression CharacterReferenceValue;
    }
}
