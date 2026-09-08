using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Novelify
{
    public partial class NovelGraphRunner
    {
        public RuntimeNovelGraph RuntimeGraph;
        private NovelStateStore _stateStore = new NovelStateStore();
        private NovelStateStore _subscribedStateStore;
        public NovelStateStore StateStore => _stateStore ??= new NovelStateStore();
        public event Action<NovelVariableDefinition, RuntimeValue> LocalVariableChanged;

        [Header("Dialogue Timing")]
        [Tooltip("Unscaled keeps conversations, waits, reveals, and character transitions running while gameplay is paused. Scaled pauses them with Time.timeScale.")]
        public DialogueTimeMode TimeMode = DialogueTimeMode.Unscaled;

        private readonly Stack<GraphCallFrame> _graphCalls = new Stack<GraphCallFrame>();
        private RuntimeValueScope _valueScope = new RuntimeValueScope();

        [Header("Sound Settings")]
        public AudioSource TalkSource;
        [Tooltip("Sound attached directly to dialogue nodes.")]
        [FormerlySerializedAs("PlaySound")]
        public AudioSource NodeSoundSource;
        [Tooltip("Sound source used by Play Sound nodes.")]
        public AudioSource PlaySoundSource;

        [Header("Character Stage")]
        public GameObject CanvasDialogue;
        public GameObject PortraitPrefab;
        [Tooltip("Optional portrait parent outside the dialogue panel. Defaults to a separate stage under the canvas.")]
        public Transform CharacterContainer;
        public bool HideCharactersOnEnd = true;

        [Header("UI Components")]
        public GameObject DialoguePanel;
        [HideInInspector] public GameObject CharacterPortrait;
        public GameObject BackgroundChoicesPanel;
        public GameObject NameBackground;
        public TextMeshProUGUI SpeakerNameText;
        public TextMeshProUGUI DialogueText;

        [Header("Choice Button UI")]
        public Button ChoiceButtonPrefab;
        public Transform ChoiceButtonContainer;

        [Header("Story Events")]
        [Tooltip("Event nodes send their Event Name to these listeners.")]
        public UnityEvent<string> OnDialogueEvent = new UnityEvent<string>();

        [Header("Extensibility")]
        [Tooltip("Optional component implementing INovelPresentation. Leave empty to use the built-in TMP/portrait presentation.")]
        [SerializeField] private MonoBehaviour presentationBehaviour;

        public IReadOnlyDictionary<string, CharacterInfo> AllCharacters => Stage.Characters;
        public bool IsWaiting => _isWaiting;
        public RuntimeNode CurrentNode => _currentNode;
        public MonoBehaviour PresentationBehaviour => presentationBehaviour;
        internal bool IsTextRevealingInternal => _isTextRevealing;
        internal bool IsGraphRunningInternal => _isGraphRunning;
        internal int GraphCallDepthInternal => _graphCalls.Count;
        protected bool HasStartedGraph => _hasStartedGraph;
        private NovelCharacterStage _stage;
        private CharacterInfo _speaker;
        private readonly Dictionary<string, RuntimeNode> _nodeLookup = new Dictionary<string, RuntimeNode>();
        private RuntimeNode _currentNode;
        private Coroutine _textRevealCoroutine;
        private Coroutine _waitCoroutine;
        private bool _isTextRevealing, _isWaiting;
        private bool _hasStartedGraph, _isGraphRunning, _ownsContainer;
        private int _nodeEnteredFrame = -1;
        private int _textCompletedFrame = -1;
        private int _flowVersion;
        private bool _choiceSelectionCommitted;
        private bool _refreshingChoices;
        private readonly Dictionary<string, Button> _choiceButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private const int MaxAutomaticNodesPerTraversal = 1000;
        private const int MaxGraphCallDepth = 128;

        private sealed class RuntimeValueScope
        {
            public readonly Dictionary<string, RuntimeValue> Inputs = new Dictionary<string, RuntimeValue>();
            public readonly Dictionary<string, RuntimeValue> Locals = new Dictionary<string, RuntimeValue>();
            internal readonly Dictionary<string, RuntimeValue> _outputs = new Dictionary<string, RuntimeValue>();

            internal static string OutputKey(string nodeID, string name) => (nodeID ?? string.Empty) + "\n" + (name ?? string.Empty);
            public void SetOutput(string nodeID, string name, RuntimeValue value) => _outputs[OutputKey(nodeID, name)] = value ?? RuntimeValue.None();
            public RuntimeValue GetOutput(string nodeID, string name) =>
                _outputs.TryGetValue(OutputKey(nodeID, name), out RuntimeValue value) ? value : RuntimeValue.None();
        }

        private readonly struct GraphCallFrame
        {
            public readonly RuntimeNovelGraph Graph;
            public readonly string ReturnNodeID;
            public readonly RuntimeValueScope Scope;
            public readonly string FunctionCallNodeID;

            public GraphCallFrame(RuntimeNovelGraph graph, string returnNodeID, RuntimeValueScope scope, string functionCallNodeID = null)
            {
                Graph = graph;
                ReturnNodeID = returnNodeID;
                Scope = scope;
                FunctionCallNodeID = functionCallNodeID;
            }
        }
    }
}
