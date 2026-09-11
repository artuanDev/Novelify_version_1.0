using UnityEngine;

namespace Novelify.Samples.TopDownKeyQuest
{
    /// <summary>Starts an authored Novelify graph when the player interacts.</summary>
    public class TopDownNovelInteractable : MonoBehaviour
    {
        public NovelGraphRunner NovelifyRunner;
        public RuntimeNovelGraph InteractionGraph;
        public string InteractionLabel = "Interact";

        public virtual string Prompt => InteractionLabel;
        public virtual bool CanInteract => isActiveAndEnabled && NovelifyRunner != null &&
                                           InteractionGraph != null && !NovelifyRunner.Session.IsRunning;

        public virtual void Interact()
        {
            if (CanInteract) NovelifyRunner.Session.Play(InteractionGraph);
        }
    }
}
