using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class UISoundLibrary : MonoBehaviour
{
    public enum SoundId { MenuIn, Back, Final}

    [Serializable]
    public class Sound
    {
        public SoundId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private Sound[] sounds;

    private AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; 
    }


    public void Play(int soundIndex) => Play((SoundId)soundIndex);

    public void Play(SoundId id)
    {
        foreach (var s in sounds)
        {
            if (s.id != id) continue;
            if (s.clip != null) source.PlayOneShot(s.clip, s.volume);
            return;
        }
        Debug.LogWarning($"UISoundLibrary: no hay sonido para {id}", this);
    }
}