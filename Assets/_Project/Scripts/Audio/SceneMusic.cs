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
        [Tooltip("Optional: the city song as stems (chords, bass, drums, lead, arp, cello, white noise). Used instead of cityTrack.")]
        public AudioClip[] cityStems = new AudioClip[0];
        public StemMix stemMix = StemMix.Street;
        [Min(0f)] public float fadeSeconds = 2.5f;

        private void Start()
        {
            MusicDirector director = MusicDirector.Ensure();
            if (fightTrack != null) director.FightTrack = fightTrack;

            if (mode == Mode.City && cityStems != null && cityStems.Length >= 7 && cityStems[0] != null) director.PlayStems(cityStems, stemMix, fadeSeconds);
            else if (mode == Mode.City && cityTrack != null) director.PlayCity(cityTrack, fadeSeconds);
            else director.StopCity(fadeSeconds);
        }
    }
}
