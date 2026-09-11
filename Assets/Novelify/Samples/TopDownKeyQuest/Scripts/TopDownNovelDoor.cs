using TMPro;
using UnityEngine;

namespace Novelify.Samples.TopDownKeyQuest
{
    /// <summary>
    /// Listens for a story event and performs the physical, game-specific result.
    /// The door never checks inventory itself; that decision stays in the graph.
    /// </summary>
    public sealed class TopDownNovelDoor : TopDownNovelInteractable
    {
        public const string OpenEvent = "topdown.door.open";

        public Collider2D BlockingCollider;
        public TMP_Text WorldLabel;
        public Vector3 OpenOffset = new Vector3(0f, 1.65f, 0f);
        [Min(0.1f)] public float OpenSpeed = 3.5f;

        private Vector3 _closedPosition;
        private Vector3 _openPosition;
        private bool _isOpen;

        public override bool CanInteract => !_isOpen && base.CanInteract;

        private void Awake()
        {
            _closedPosition = transform.position;
            _openPosition = _closedPosition + OpenOffset;
        }

        private void OnEnable()
        {
            if (NovelifyRunner != null) NovelifyRunner.Session.EventRaised += OnNovelifyEvent;
        }

        private void OnDisable()
        {
            if (NovelifyRunner != null) NovelifyRunner.Session.EventRaised -= OnNovelifyEvent;
        }

        private void Update()
        {
            if (_isOpen) transform.position = Vector3.MoveTowards(transform.position, _openPosition, OpenSpeed * Time.deltaTime);
        }

        private void OnNovelifyEvent(string eventName)
        {
            if (eventName != OpenEvent || _isOpen) return;
            _isOpen = true;
            if (BlockingCollider != null) BlockingCollider.enabled = false;
            if (WorldLabel != null) WorldLabel.text = "OPEN";
        }
    }
}
