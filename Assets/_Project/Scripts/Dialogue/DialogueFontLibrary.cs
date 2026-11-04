using UnityEngine;

namespace Subnet.Dialogue
{
    /// <summary>
    /// List of every font in the project, filled by Tools → Subnet → Build Full Game and stored in Resources,
    /// so font files can live in any folder and still ship in builds. DialogueFonts picks voices from it by name.
    /// </summary>
    public class DialogueFontLibrary : ScriptableObject
    {
        public Font[] fonts = new Font[0];
    }
}
