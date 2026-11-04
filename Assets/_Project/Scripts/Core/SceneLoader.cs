using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Subnet.Core
{
    /// <summary>
    /// Fade-out → async load → checkpoint/chapter bookkeeping. Usable from anywhere:
    /// SceneLoader.Load("02_Station", "ch2");
    /// </summary>
    public static class SceneLoader
    {
        public const string MenuScene = "00_MainMenu";

        private static bool loading;

        /// <summary>True while a transition is running.</summary>
        public static bool IsLoading => loading;

        /// <summary>Loads <paramref name="sceneName"/> behind a fade. Unlocks a chapter and saves a checkpoint.</summary>
        public static void Load(string sceneName, string unlockChapterKey = null, bool saveCheckpoint = true, float fadeDuration = 0.9f)
        {
            if (loading) return;
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not in the Build Profile scene list. Run Tools → Subnet → Build Full Game.");
                return;
            }

            loading = true;
            GameTime.SetPaused(false);
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(unlockChapterKey)) SaveData.UnlockChapter(unlockChapterKey);
            if (saveCheckpoint && sceneName != MenuScene) SaveData.SetCheckpoint(sceneName);

            var runner = new GameObject("[SceneLoader]").AddComponent<SceneLoaderRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(Routine(sceneName, runner, fadeDuration));
        }

        private static IEnumerator Routine(string sceneName, SceneLoaderRunner runner, float fadeDuration)
        {
            InputLock.Acquire(runner);

            ScreenFader fader = Object.FindFirstObjectByType<ScreenFader>();
            if (fader != null) yield return fader.FadeTo(1f, fadeDuration);
            else
            {
                for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime) yield return null;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            while (!operation.isDone) yield return null;

            InputLock.ReleaseAll();
            loading = false;
            Object.Destroy(runner.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => loading = false;
    }
}
