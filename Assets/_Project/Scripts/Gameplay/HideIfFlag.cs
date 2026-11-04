using Subnet.Core;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>Switches this object off when the scene starts if a story flag is already set (Byte after the Ghost Line).</summary>
    public class HideIfFlag : MonoBehaviour
    {
        [SerializeField] private string flag = string.Empty;

        private void Start()
        {
            if (!string.IsNullOrEmpty(flag) && StoryFlags.Has(flag)) gameObject.SetActive(false);
        }
    }
}
