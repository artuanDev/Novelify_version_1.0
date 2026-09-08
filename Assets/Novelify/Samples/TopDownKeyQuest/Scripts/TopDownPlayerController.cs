using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Novelify.Samples.TopDownKeyQuest
{
    /// <summary>
    /// The only player controller needed by this sample. Novelify owns the story;
    /// this component owns movement, proximity, and forwarding player intent.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class TopDownPlayerController : MonoBehaviour
    {
        [Header("Game references")]
        public NovelGraphRunner NovelifyRunner;
        public TMP_Text InteractionPrompt;

        [Header("Movement and interaction")]
        [Min(0f)] public float MoveSpeed = 4.5f;
        [Min(0.1f)] public float InteractionRadius = 1.35f;

        private Rigidbody2D _body;
        private Vector2 _movement;
        private TopDownNovelInteractable _nearby;

        private void Awake() => _body = GetComponent<Rigidbody2D>();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (NovelifyRunner != null && NovelifyRunner.Session.IsRunning)
            {
                _movement = Vector2.zero;
                _nearby = null;
                SetPrompt(string.Empty);

                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
                    Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    NovelifyRunner.Session.Advance();
                }
                return;
            }

            _movement = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)).normalized;

            _nearby = FindClosestInteractable();
            SetPrompt(_nearby != null ? $"[E]  {_nearby.Prompt}" : string.Empty);
            if (_nearby != null && keyboard.eKey.wasPressedThisFrame) _nearby.Interact();
        }

        private void FixedUpdate()
        {
            _body.linearVelocity = _movement * MoveSpeed;
        }

        private TopDownNovelInteractable FindClosestInteractable()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, InteractionRadius);
            TopDownNovelInteractable closest = null;
            float closestDistance = float.PositiveInfinity;
            foreach (Collider2D hit in hits)
            {
                TopDownNovelInteractable candidate = hit.GetComponentInParent<TopDownNovelInteractable>();
                if (candidate == null || !candidate.CanInteract) continue;
                float distance = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closest = candidate;
                closestDistance = distance;
            }
            return closest;
        }

        private void SetPrompt(string value)
        {
            if (InteractionPrompt == null) return;
            InteractionPrompt.text = value;
            InteractionPrompt.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.98f, 0.82f, 0.3f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, InteractionRadius);
        }
    }
}
