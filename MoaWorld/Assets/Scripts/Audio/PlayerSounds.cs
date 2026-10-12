using UnityEngine;

namespace MoaWorld
{
    // 3D sound effects at the player, heard by everyone nearby. Driven by the same events as the animations.
    [RequireComponent(typeof(PlayerCapture))]
    public class PlayerSounds : MonoBehaviour
    {
        [SerializeField] private AudioClip ballThrow;

        private AudioSource source;

        private void Awake()
        {
            GameConfig config = GameConfig.Instance;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = config.sfxFullVolumeDistance;
            source.maxDistance = config.sfxMaxDistance;

            GetComponent<PlayerCapture>().Thrown += OnThrown;
        }

        private void OnDestroy()
        {
            GetComponent<PlayerCapture>().Thrown -= OnThrown;
        }

        private void OnThrown()
        {
            source.PlayOneShot(ballThrow, GameConfig.Instance.ballThrowVolume);
        }
    }
}
