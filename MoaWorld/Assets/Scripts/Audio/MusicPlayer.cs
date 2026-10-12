using UnityEngine;

namespace MoaWorld
{
    // Background music, looping from the lobby through the game. Local only.
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip music;

        private void Start()
        {
            AudioSource source = GetComponent<AudioSource>();
            source.clip = music;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = GameConfig.Instance.musicVolume;
            source.Play();
        }
    }
}
