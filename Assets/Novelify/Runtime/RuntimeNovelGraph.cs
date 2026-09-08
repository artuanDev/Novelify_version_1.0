using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    public class RuntimeNovelGraph : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [Tooltip("Persistent identity of the authored graph asset.")]
        public string GraphID;
        [Tooltip("Hash of the authored graph content at import time.")]
        public string ContentVersion;
        public int SchemaVersion = CurrentSchemaVersion;
        public string EntryNodeID;

        // Required so Unity preserves RuntimeDialogueNode,
        // RuntimeChoiceNode, RuntimePlaySoundNode, etc.
        [SerializeReference]
        public List<RuntimeNode> AllNodes = new List<RuntimeNode>();
    }

    public enum RuntimeValueKind
    {
        None,
        Float,
        Integer,
        Boolean,
        String,
        Vector2,
        Object,
        CharacterReference
    }

    [Serializable]
    public struct NovelCharacterReference
    {
        public NovelCharacter Character;
        public string InstanceID;

        public NovelCharacterReference(NovelCharacter character, string instanceID = "")
        {
            Character = character;
            InstanceID = instanceID ?? string.Empty;
        }
    }

    public enum CharacterPositionSpace { Canvas, Normalized }
    public enum CharacterFacing { Left, Right }
    public enum DialogueTimeMode { Unscaled, Scaled }

    [Serializable]
    public class RuntimeValue
    {
        public RuntimeValueKind Kind;
        public float FloatValue;
        public int IntegerValue;
        public bool BooleanValue;
        public string StringValue;
        public Vector2 Vector2Value;
        public UnityEngine.Object ObjectValue;
        public NovelCharacterReference CharacterReferenceValue;

        public static RuntimeValue None() => new RuntimeValue();
        public static RuntimeValue From(float value) => new RuntimeValue { Kind = RuntimeValueKind.Float, FloatValue = value };
        public static RuntimeValue From(int value) => new RuntimeValue { Kind = RuntimeValueKind.Integer, IntegerValue = value };
        public static RuntimeValue From(bool value) => new RuntimeValue { Kind = RuntimeValueKind.Boolean, BooleanValue = value };
        public static RuntimeValue From(string value) => new RuntimeValue { Kind = RuntimeValueKind.String, StringValue = value ?? string.Empty };
        public static RuntimeValue From(Vector2 value) => new RuntimeValue { Kind = RuntimeValueKind.Vector2, Vector2Value = value };
        public static RuntimeValue From(UnityEngine.Object value) => new RuntimeValue { Kind = RuntimeValueKind.Object, ObjectValue = value };
        public static RuntimeValue From(NovelCharacterReference value) => new RuntimeValue
            { Kind = RuntimeValueKind.CharacterReference, CharacterReferenceValue = value };
    }

    [Serializable]
    public abstract class RuntimeValueExpression { }

    [Serializable]
    public class RuntimeConstantExpression : RuntimeValueExpression
    {
        public RuntimeValue Value = RuntimeValue.None();
    }

    [Serializable]
    public class RuntimeFunctionInputExpression : RuntimeValueExpression
    {
        public string Name;
    }

    [Serializable]
    public class RuntimeFunctionOutputExpression : RuntimeValueExpression
    {
        public string CallNodeID;
        public string Name;
    }

    public enum RuntimeArithmeticOperation { Add, Subtract, Multiply, Divide }
    public enum RuntimeComparisonOperation { Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual }
    public enum RuntimeBooleanOperation { And, Or, Not }
    public enum RuntimeVariableModifyOperation { Add, Subtract, Multiply, Divide }

    [Serializable]
    public class RuntimeArithmeticExpression : RuntimeValueExpression
    {
        public RuntimeArithmeticOperation Operation;
        public RuntimeValueKind ValueKind;
        [SerializeReference] public RuntimeValueExpression A;
        [SerializeReference] public RuntimeValueExpression B;
    }

    [Serializable]
    public class RuntimeVariableExpression : RuntimeValueExpression
    {
        public NovelVariableDefinition Variable;
    }

    [Serializable]
    public class RuntimeComparisonExpression : RuntimeValueExpression
    {
        public RuntimeComparisonOperation Operation;
        public RuntimeValueKind ValueKind;
        [SerializeReference] public RuntimeValueExpression A;
        [SerializeReference] public RuntimeValueExpression B;
    }

    [Serializable]
    public class RuntimeBooleanExpression : RuntimeValueExpression
    {
        public RuntimeBooleanOperation Operation;
        [SerializeReference] public RuntimeValueExpression A;
        [SerializeReference] public RuntimeValueExpression B;
    }

    public enum RuntimeCharacterComponent
    {
        Character,
        SpeakerName,
        Body,
        Eyes,
        EyesClosed,
        Details,
        Mouth,
        MouthOpen,
        NormalizedPosition,
        CanvasPosition,
        Rotation,
        Scale
    }

    [Serializable]
    public class RuntimeCharacterComponentExpression : RuntimeValueExpression
    {
        public RuntimeCharacterComponent Component;
        [SerializeReference] public RuntimeValueExpression Character;
        [SerializeReference] public RuntimeValueExpression InstanceID;
    }

    [Serializable]
    public class RuntimeMakeCharacterReferenceExpression : RuntimeValueExpression
    {
        [SerializeReference] public RuntimeValueExpression Character;
        [SerializeReference] public RuntimeValueExpression InstanceID;
    }

    public enum RuntimeCharacterReferenceComponent { Character, InstanceID }

    [Serializable]
    public class RuntimeCharacterReferenceComponentExpression : RuntimeValueExpression
    {
        public RuntimeCharacterReferenceComponent Component;
        [SerializeReference] public RuntimeValueExpression Reference;
    }

    [Serializable]
    public class RuntimeFunctionInput
    {
        public string Name;
        public RuntimeValue DefaultValue = RuntimeValue.None();
    }

    [Serializable]
    public class RuntimeFunctionOutput
    {
        public string Name;
        [SerializeReference] public RuntimeValueExpression Value;
    }

    [Serializable]
    public class RuntimeFunctionArgument
    {
        public string Name;
        [SerializeReference] public RuntimeValueExpression Value;
    }

    [Serializable]
    public class RuntimeNode
    {
        public string NodeID;
        public string NextNodeID;
    }

    [Serializable]
    public class RuntimeDialogueNode : RuntimeNode
    {
        public NovelCharacter NovelCharacter;
        [SerializeReference] public RuntimeValueExpression CharacterValue;
        [SerializeReference] public RuntimeValueExpression CharacterReferenceValue;
        public string InstanceID;
        public string SpeakerName;

        public Sprite PortraitBody;
        public Sprite PortraitDetails;
        public Sprite PortraitEyes;
        public Sprite PortraitEyesClosed;
        public Sprite PortraitMouth;
        public Sprite PortraitMouthOpen;

        public string DialogueText;

        public bool ShowTextImmediately;
        public float CharactersPerSecond = 30f;
        public bool AnimateMouth = true;
        public bool AnimateBlinking = true;
        public CharacterEmotion Emotion = CharacterEmotion.Neutral;

        public float MouthFrameInterval = 0.12f;
        public float MouthTimingVariation = 0.35f;
        public float MouthPauseChance = 0.12f;
        public float MouthPauseMultiplier = 1.8f;

        public float BlinkIntervalMin = 2.5f;
        public float BlinkIntervalMax = 5f;
        public float BlinkDuration = 0.12f;

        public AudioClip TalkSound;
        public AudioClip PlaySound;
        [SerializeReference] public RuntimeValueExpression PlaySoundValue;

        public float PitchMinVariation = -0.05f;
        public float PitchMaxVariation = 0.05f;
    }

    // A choice node contains dialogue presentation data,
    // plus its available choices.
    [Serializable]
    public class RuntimeChoiceNode : RuntimeDialogueNode
    {
        public List<ChoiceData> Choices = new List<ChoiceData>();
    }

    [Serializable]
    public class RuntimePlaySoundNode : RuntimeNode
    {
        public bool Loop;
        public AudioClip ClipSound;
        [SerializeReference] public RuntimeValueExpression ClipValue;

        public float Volume = 1f;
        public int Priority = 128;
        public float Pitch = 1f;
    }

    [Serializable]
    public class RuntimeTransformSpeakerPortraitNode : RuntimeNode
    {
        public NovelCharacter Character;
        public string InstanceID;
        public float OffsetX;
        public float OffsetY;
        public float Rotation;
        public Vector2 Scale = Vector2.one;
        public float Margin;
        public bool PositionIsNormalized = true;
        public CharacterPositionSpace PositionSpace = CharacterPositionSpace.Normalized;
        public bool SmoothMovement;
        public float Duration = 0.5f;
        public bool WaitForCompletion = true;
        public bool EaseInOut = true;
        public bool Relative;

        [SerializeReference] public RuntimeValueExpression CharacterValue;
        [SerializeReference] public RuntimeValueExpression CharacterReferenceValue;
        [SerializeReference] public RuntimeValueExpression PositionValue;
        [SerializeReference] public RuntimeValueExpression RotationValue;
        [SerializeReference] public RuntimeValueExpression ScaleValue;
        [SerializeReference] public RuntimeValueExpression MarginValue;
    }

    [Serializable]
    public class RuntimeSetVariableNode : RuntimeNode
    {
        public NovelVariableDefinition Variable;
        [SerializeReference] public RuntimeValueExpression Value;
    }

    [Serializable]
    public class RuntimeModifyVariableNode : RuntimeNode
    {
        public NovelVariableDefinition Variable;
        public RuntimeVariableModifyOperation Operation;
        [SerializeReference] public RuntimeValueExpression Amount;
    }

    [Serializable]
    public class RuntimeBranchNode : RuntimeNode
    {
        [SerializeReference] public RuntimeValueExpression Condition;
        public string TrueNodeID;
        public string FalseNodeID;
    }

    [Serializable]
    public class RuntimeTranslateSpeakerPortraitNode : RuntimeTransformSpeakerPortraitNode
    {
        public RuntimeTranslateSpeakerPortraitNode()
        {
            PositionIsNormalized = false;
            PositionSpace = CharacterPositionSpace.Canvas;
        }
    }

    [Serializable]
    public class RuntimeFlipCharacterNode : RuntimeNode
    {
        public string InstanceID;
        public NovelCharacter Character;
        public bool FlipX;
        public bool FlipY;
        [SerializeReference] public RuntimeValueExpression CharacterValue;
        [SerializeReference] public RuntimeValueExpression CharacterReferenceValue;
    }

    [Serializable]
    public class RuntimeSetCharacterFacingNode : RuntimeNode
    {
        public string InstanceID;
        public NovelCharacter Character;
        public CharacterFacing Facing = CharacterFacing.Right;
        [SerializeReference] public RuntimeValueExpression CharacterValue;
        [SerializeReference] public RuntimeValueExpression CharacterReferenceValue;
    }

    [Serializable]
    public class RuntimeShowCharacterNode : RuntimeNode
    {
        public NovelCharacter Character;
        public string InstanceID;
        public Vector2 Position;
        public CharacterPositionSpace PositionSpace = CharacterPositionSpace.Canvas;
        public CharacterEmotion Emotion;
        [SerializeReference] public RuntimeValueExpression CharacterValue;
        [SerializeReference] public RuntimeValueExpression CharacterReferenceValue;
        [SerializeReference] public RuntimeValueExpression PositionValue;
    }

    [Serializable]
    public class RuntimeHideCharacterNode : RuntimeNode
    {
        public NovelCharacter Character;
        public string InstanceID;
        [SerializeReference] public RuntimeValueExpression CharacterValue;
        [SerializeReference] public RuntimeValueExpression CharacterReferenceValue;
    }

    [Serializable]
    public class RuntimeHideAllCharactersNode : RuntimeNode { }

    [Serializable]
    public class RuntimeSetCharacterEmotionNode : RuntimeNode
    {
        public NovelCharacter Character;
        public string InstanceID;
        public CharacterEmotion Emotion;
        [SerializeReference] public RuntimeValueExpression CharacterValue;
        [SerializeReference] public RuntimeValueExpression CharacterReferenceValue;
    }

    [Serializable]
    public class RuntimeWaitNode : RuntimeNode
    {
        public float Duration = 1f;
    }

    [Serializable]
    public class RuntimeDialogueEventNode : RuntimeNode
    {
        public string EventName;
    }

    [Serializable]
    public class RuntimeStopSoundNode : RuntimeNode { }

    [Serializable]
    public class RuntimeCallNovelPageNode : RuntimeNode
    {
        public RuntimeNovelGraph Graph;
    }

    [Serializable]
    public class RuntimeCallNovelFunctionNode : RuntimeNode
    {
        public RuntimeNovelFunction Function;
        public List<RuntimeFunctionArgument> Arguments = new List<RuntimeFunctionArgument>();
    }

    [Serializable]
    public class ChoiceData
    {
        public string ChoiceText;
        [SerializeReference] public RuntimeValueExpression ChoiceTextValue;
        public string DestinationNodeID;
    }
}
