using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Audio/Scene Music Config", fileName = "SceneMusicConfig")]
public sealed class SceneMusicConfig : ScriptableObject
{
    [Serializable]
    public sealed class SceneTrack
    {
        public string sceneName;
        public AudioClip clip;

        [Range(0f, 1f)]
        public float baseVolume = 1f;

        [Header("Day/Night Override")]
        public bool useDayNightCycle;
        public AudioClip dayClip;
        public AudioClip nightClip;

        [Range(0f, 1f)]
        public float dayBaseVolume = 1f;

        [Range(0f, 1f)]
        public float nightBaseVolume = 1f;

        [Range(0f, 1f)]
        public float nightSwitchThreshold = 0.5f;
    }

    [Tooltip("One entry per scene that should have looping background music.")]
    public SceneTrack[] tracks;

    [Tooltip("Stops the current music when a loaded scene has no configured track.")]
    public bool stopWhenSceneHasNoTrack = true;

    [Tooltip("Log a warning in editor when a scene has no valid configured track.")]
    public bool warnMissingTracks = true;

    public SceneTrack FindTrack(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || tracks == null)
            return null;

        for (int i = 0; i < tracks.Length; i++)
        {
            SceneTrack track = tracks[i];
            if (track == null)
                continue;

            if (string.Equals(track.sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                return track;
        }

        return null;
    }

    private void OnValidate()
    {
        if (tracks == null)
            return;

        for (int i = 0; i < tracks.Length; i++)
        {
            if (tracks[i] == null)
                continue;

            tracks[i].baseVolume = Mathf.Clamp01(tracks[i].baseVolume);
            tracks[i].dayBaseVolume = Mathf.Clamp01(tracks[i].dayBaseVolume);
            tracks[i].nightBaseVolume = Mathf.Clamp01(tracks[i].nightBaseVolume);
            tracks[i].nightSwitchThreshold = Mathf.Clamp01(tracks[i].nightSwitchThreshold);
        }
    }
}
