using Subnet.Interaction;
using Subnet.Player;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>A lamp / lever that belongs to a SequencePuzzle. Needs a Collider on the Interactable layer.</summary>
    public class PuzzleSwitch : MonoBehaviour, IInteractable
    {
        [SerializeField] private Light switchLight;
        [Tooltip("Optional renderer whose emission brightens when lit.")]
        [SerializeField] private Renderer glowRenderer;
        [SerializeField] private Color litColor = new Color(1f, 0.85f, 0.55f);
        [SerializeField] private Transform promptAnchor;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private SequencePuzzle owner;
        private MaterialPropertyBlock block;

        public bool IsLit { get; private set; }

        public bool CanInteract => isActiveAndEnabled && owner != null && !owner.IsSolved && !IsLit;

        public Vector3 PromptAnchor => promptAnchor != null ? promptAnchor.position : transform.position + Vector3.up * 1.6f;

        public void Bind(SequencePuzzle puzzle) => owner = puzzle;

        private void Awake()
        {
            SetLit(false);
        }

        public void SetFocused(bool focused) { }

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor != null) interactor.FaceTowards(transform.position);
            if (owner != null) owner.Report(this);
        }

        public void SetLit(bool lit)
        {
            IsLit = lit;
            if (switchLight != null) switchLight.enabled = lit;
            if (glowRenderer != null)
            {
                block ??= new MaterialPropertyBlock();
                glowRenderer.GetPropertyBlock(block);
                block.SetColor(EmissionId, lit ? litColor * 2.5f : Color.black);
                glowRenderer.SetPropertyBlock(block);
            }
        }
    }
}
