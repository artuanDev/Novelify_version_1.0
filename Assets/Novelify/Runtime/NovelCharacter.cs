using UnityEngine;

namespace Novelify
{
    [CreateAssetMenu(
        fileName = "New Novel Character",
        menuName = "Novelify/Character",
        order = 0)]
    public class NovelCharacter : ScriptableObject
    {
        [Header("Dialogue")]
        [Tooltip("Name displayed by the Novel Manager while this character is speaking.")]
        public string SpeakerName;

        [Tooltip("Portrait displayed in the graph preview and by the Novel Manager at runtime.")]
        public Sprite PortraitBody;
        public Sprite PortraitMouth;
        public Sprite PortraitFaceDetails;
        public Sprite PortraitEyes;

        [Header("Graph Preview")]
        [Range(80f, 220f)]
        [Tooltip("Width of this character's portrait preview inside Dialogue and Choice nodes.")]
        public float PreviewWidth = 100f;

        [Range(60f, 220f)]
        [Tooltip("Height of this character's portrait preview inside Dialogue and Choice nodes.")]
        public float PreviewHeight = 150f;

        [Range(1f, 4f)]
        [Tooltip("Zoom used to crop the portrait inside its graph preview frame.")]
        public float PreviewZoom = 1f;

        [Range(-1f, 1f)]
        [Tooltip("Positive values move the portrait right; negative values move it left.")]
        public float PreviewOffsetX;

        [Range(-1f, 1f)]
        [Tooltip("Positive values move the portrait up; negative values move it down.")]
        public float PreviewOffsetY;
    }
}
