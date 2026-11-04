using UnityEngine;

namespace Subnet.Core
{
    /// <summary>
    /// Minimal checkpoint + chapter unlock persistence. The game is short (15–30 min),
    /// so a scene-level checkpoint plus unlocked "Corrupted Memories" is all we need.
    /// </summary>
    public static class SaveData
    {
        private const string KeyCheckpointScene = "save.checkpointScene";
        private const string KeyChapterPrefix = "save.chapter.";

        /// <summary>True if the player has reached at least one checkpoint.</summary>
        public static bool HasSave => !string.IsNullOrEmpty(CheckpointScene);

        /// <summary>Name of the last scene the player checkpointed in.</summary>
        public static string CheckpointScene => PlayerPrefs.GetString(KeyCheckpointScene, string.Empty);

        public static void SetCheckpoint(string sceneName)
        {
            PlayerPrefs.SetString(KeyCheckpointScene, sceneName);
            StoryFlags.SaveToPrefs();
            PlayerPrefs.Save();
        }

        public static void UnlockChapter(string chapterKey)
        {
            if (string.IsNullOrEmpty(chapterKey)) return;
            PlayerPrefs.SetInt(KeyChapterPrefix + chapterKey, 1);
            PlayerPrefs.Save();
        }

        public static bool IsChapterUnlocked(string chapterKey)
        {
            return string.IsNullOrEmpty(chapterKey) || PlayerPrefs.GetInt(KeyChapterPrefix + chapterKey, 0) == 1;
        }

        /// <summary>Wipes the checkpoint (chapter unlocks are kept, like a gallery).</summary>
        public static void ClearCheckpoint()
        {
            PlayerPrefs.DeleteKey(KeyCheckpointScene);
            PlayerPrefs.Save();
        }
    }
}
