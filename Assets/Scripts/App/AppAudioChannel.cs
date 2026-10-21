using UnityEngine;

// Add beside an AudioSource when music/SFX assets are introduced.
[RequireComponent(typeof(AudioSource))]
public sealed class AppAudioChannel : MonoBehaviour
{
    public enum Channel { Music, Effects }
    [SerializeField] private Channel channel;
    [SerializeField, Range(0, 1)] private float baseVolume = 1;
    private void OnEnable() { AppAudioSettings.Changed += Refresh; Refresh(); }
    private void OnDisable() => AppAudioSettings.Changed -= Refresh;
    private void Refresh() => GetComponent<AudioSource>().volume = baseVolume *
        (channel == Channel.Music ? AppAudioSettings.MusicVolume : AppAudioSettings.EffectsVolume);
}
