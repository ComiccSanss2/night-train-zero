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
        [Tooltip("Optional: a note this switch plays when touched (bottles, drums), pitch set on the source.")]
        [SerializeField] private AudioSource noteSource;
        [Tooltip("Optional: what bounces when touched.")]
        [SerializeField] private Transform punch;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private SequencePuzzle owner;
        private MaterialPropertyBlock block;

        public bool IsLit { get; private set; }

        public bool CanInteract => isActiveAndEnabled && owner != null && !owner.IsSolved && !IsLit;

        public Vector3 PromptAnchor => promptAnchor != null ? promptAnchor.position : transform.position + Vector3.up * 1.6f;

        public void Bind(SequencePuzzle puzzle) => owner = puzzle;

        private void Awake()
        {
            if (punch != null) { punchBase = punch.localScale; punchBaseSet = true; }
            SetLit(false);
        }

        public void SetFocused(bool focused) { }

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor != null) interactor.FaceTowards(transform.position);
            Hit();
        }

        public SequencePuzzle Owner => owner;

        /// <summary>Plays this switch (note, bounce) and reports it to the puzzle. Also used by InstrumentPlayer.</summary>
        public void Hit()
        {
            if (owner != null && owner.IsLocked) { owner.SayLocked(); return; }
            if (noteSource != null) noteSource.Play();
            if (punch != null)
            {
                if (punching != null) StopCoroutine(punching);
                punching = StartCoroutine(Punch());
            }
            if (owner != null && !owner.IsSolved && !IsLit) owner.Report(this);
        }

        private Vector3 punchBase;
        private bool punchBaseSet;
        private Coroutine punching;

        private System.Collections.IEnumerator Punch()
        {
            if (!punchBaseSet) { punchBase = punch.localScale; punchBaseSet = true; }
            Vector3 baseScale = punchBase;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Sin(t / 0.25f * Mathf.PI);
                punch.localScale = new Vector3(baseScale.x * (1f + 0.12f * k), baseScale.y * (1f - 0.1f * k), baseScale.z * (1f + 0.12f * k));
                yield return null;
            }
            punch.localScale = baseScale;
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
