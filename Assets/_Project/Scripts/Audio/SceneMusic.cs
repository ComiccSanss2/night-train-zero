using UnityEngine;

namespace Subnet.Audio
{
    /// <summary>
    /// Tells the persistent MusicDirector what this scene wants: the city track (continues seamlessly
    /// from the previous scene if it's the same clip) or silence. Also registers the fight track.
    /// </summary>
    public class SceneMusic : MonoBehaviour
    {
        public enum Mode { City, Silence }

        public Mode mode = Mode.City;
        public AudioClip cityTrack;
        public AudioClip fightTrack;
        [Min(0f)] public float fadeSeconds = 2.5f;

        private void Start()
        {
            MusicDirector director = MusicDirector.Ensure();
            if (fightTrack != null) director.FightTrack = fightTrack;

            if (mode == Mode.City && cityTrack != null) director.PlayCity(cityTrack, fadeSeconds);
            else director.StopCity(fadeSeconds);
        }
    }
}
