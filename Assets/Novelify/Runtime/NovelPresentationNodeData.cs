using System;
using System.Security.Cryptography.X509Certificates;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine;

namespace Novelify
{
    //Which channel of audio the play audio node will affect
    public enum NovelAudioChannel
    {
        Music,
        Ambience,
        SoundEffects,
        Voice,
        UI
    }

    //Changing the style of a dialogue box using a target so the logic can be reused
    public enum NovelBoxTarget
    {
        Dialogue,
        Speaker,
        Both
    }

    //Types of fade transition in the novel screen
    public enum NovelFadeEasing
    {
        Linear,
        EaseIn,
        EaseOut,
        EasiInOut
    }

    //Struct that will hold the novel boxes information available to edit
    [Serializable]
    public struct NovelBoxStyle
    {
        public Color FillColor;
        public float Opacity;
        public float CornerRadius;
        public bool OutlineEnabled;
        public Color OutlineColor;
        public float OutlineThickness;

        public NovelBoxStyle Validated()
        {
            NovelBoxStyle value = this;
            value.Opacity = Mathf.Clamp01(value.Opacity);
            value.CornerRadius = Mathf.Max(0f, value.CornerRadius);
            value.OutlineThickness = Mathf.Max(0f, value.OutlineThickness);
            return value;
        }

        public Color EffectiveFillColor
        {
            get
            {
                NovelBoxStyle value = Validated();
                Color color = value.FillColor;
                color.a *= value.Opacity;
                return color;
            }
        }

        //Default styles
        public static NovelBoxStyle DialogueDefault = new NovelBoxStyle
        {
            FillColor = new Color(0.055f, 0.075f, 0.13f, 1f),
            Opacity = 0.94f,
            CornerRadius = 24f,
            OutlineEnabled = false,
            OutlineColor = new Color(1f, 1f, 1f, 0.35f),
            OutlineThickness = 2f,
        };

        public static NovelBoxStyle SpeakerDefault => new NovelBoxStyle
        {
            FillColor = new Color(0.12f, 0.42f, 0.88f, 1f),
            Opacity = 1f,
            CornerRadius = 16f,
            OutlineEnabled = false,
            OutlineColor = Color.white,
            OutlineThickness = 2f,
        };
    }

    //Create a dialogue box in runtime
    [Serializable]
    public sealed class RuntimeCreateDialogueBoxNode : RuntimeNode
    {
        public NovelBoxStyle Style = NovelBoxStyle.DialogueDefault;
        public float Height = 180f;
        public float BottomMargin = 32f;
        public float HorizontalMargin = 48f;
        public float HorizontalPadding = 32f;
        public float VerticalPadding = 22f;
    }

    [Serializable]
    public sealed class RuntimeCreateDialogueSpeakerBoxNode : RuntimeNode
    {
        public NovelBoxStyle Style = NovelBoxStyle.SpeakerDefault;
        public float Width = 260f;
        public float Height = 54f;
        public float HorizontalOffset = 24f;
        public float VerticalOverlap = 27f;
    }

    [Serializable]
    public sealed class RuntimeChangeDialogueStyleNode : RuntimeNode
    {
        public NovelBoxTarget Target = NovelBoxTarget.Both;
        public NovelBoxStyle Style = NovelBoxStyle.DialogueDefault;
    }
}
