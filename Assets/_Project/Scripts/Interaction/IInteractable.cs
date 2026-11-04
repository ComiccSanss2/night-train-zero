using UnityEngine;

namespace Subnet.Interaction
{
    /// <summary>
    /// Anything Leo can interact with: inspectable props, NPCs, doors, hacking terminals.
    /// Implementers need a Collider on the "Interactable" layer (on themselves or a child).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>False hides the prompt and ignores the interact button.</summary>
        bool CanInteract { get; }

        /// <summary>World position the interaction prompt floats above.</summary>
        Vector3 PromptAnchor { get; }

        /// <summary>Called when the interactor becomes / stops being the current focus.</summary>
        void SetFocused(bool focused);

        /// <summary>Executes the interaction.</summary>
        void Interact(Player.PlayerInteractor interactor);
    }
}
