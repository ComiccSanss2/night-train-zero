#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Subnet.EditorTools
{
    /// <summary>
    /// One-click project scaffolding: Tools → Subnet → Setup Project Structure.
    /// Creates the folder architecture plus the Tags and Layers the Step 1 scripts expect.
    /// Safe to run repeatedly (it never deletes or renames anything).
    /// </summary>
    public static class SubnetProjectSetup
    {
        private static readonly string[] Folders =
        {
            "Assets/_Project/Art/Materials/Reality",
            "Assets/_Project/Art/Materials/Subnet",
            "Assets/_Project/Art/Shaders",
            "Assets/_Project/Art/Fonts",
            "Assets/_Project/Art/Sprites/UI",
            "Assets/_Project/Art/Sprites/Portraits",
            "Assets/_Project/Art/VFX",
            "Assets/_Project/Animation/Controllers",
            "Assets/_Project/Animation/Clips",
            "Assets/_Project/Audio/Mixers",
            "Assets/_Project/Audio/Music",
            "Assets/_Project/Audio/Ambience",
            "Assets/_Project/Audio/SFX/UI",
            "Assets/_Project/Audio/SFX/Footsteps",
            "Assets/_Project/Audio/SFX/Hacking",
            "Assets/_Project/Data/Dialogue/Act1_Tenement",
            "Assets/_Project/Data/Dialogue/Act2_Commercial",
            "Assets/_Project/Data/Dialogue/Act3_ConcretePark",
            "Assets/_Project/Data/Dialogue/Act4_CentralCore",
            "Assets/_Project/Data/Speakers",
            "Assets/_Project/Prefabs/Characters",
            "Assets/_Project/Prefabs/Props/Inspectables",
            "Assets/_Project/Prefabs/Environment",
            "Assets/_Project/Prefabs/UI",
            "Assets/_Project/Prefabs/Systems",
            "Assets/_Project/Scenes",
            "Assets/_Project/Settings/PostProcessing",
            "Assets/_Project/Timeline",
        };

        private static readonly string[] Tags = { "Inspectable", "NPC", "CinematicPoint", "Obstacle" };

        // Layer index → name. Indices 8+ are user layers.
        private static readonly (int index, string name)[] Layers =
        {
            (8, "Player"),
            (9, "Interactable"),
            (10, "Ground"),
            (11, "Obstacle"),
            (12, "NPC"),
        };

        [MenuItem("Tools/Subnet/Setup Project Structure")]
        public static void Setup()
        {
            foreach (string folder in Folders) EnsureFolder(folder);

            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            AddTags(tagManager);
            AddLayers(tagManager);
            tagManager.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Subnet] Project structure, tags and layers are ready.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void AddTags(SerializedObject tagManager)
        {
            SerializedProperty tags = tagManager.FindProperty("tags");
            foreach (string tag in Tags)
            {
                bool exists = false;
                for (int i = 0; i < tags.arraySize; i++)
                {
                    if (tags.GetArrayElementAtIndex(i).stringValue == tag) { exists = true; break; }
                }
                if (exists) continue;
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            }
        }

        private static void AddLayers(SerializedObject tagManager)
        {
            SerializedProperty layers = tagManager.FindProperty("layers");
            foreach ((int index, string name) in Layers)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = name;
                }
                else if (slot.stringValue != name)
                {
                    Debug.LogWarning($"[Subnet] Layer {index} is already '{slot.stringValue}'. Add '{name}' manually in a free slot.");
                }
            }
        }
    }
}
#endif
