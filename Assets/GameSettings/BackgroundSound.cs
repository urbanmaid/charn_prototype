using System.Collections.Generic;
using UnityEngine;

public class BackgroundSound : MonoBehaviour
{
    public List<AudioClip> bgmClips;
    private int bgmClipsIndex;
    private AudioSource audioSource;
    private StageSceneInitializer stageInitializer;
    private bool stageLoop;
    private float originalVolume;
    private float originalSpatialBlend;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        originalVolume = audioSource.volume;
        originalSpatialBlend = audioSource.spatialBlend;
        audioSource.loop = false;
        audioSource.playOnAwake = false;
    }
    void OnEnable() { CombatAudioFocus.RegisterBackground(GetComponent<AudioSource>()); }
    void OnDisable() { CombatAudioFocus.UnregisterBackground(audioSource); }
    void Start()
    {
        stageInitializer = FindAnyObjectByType<StageSceneInitializer>();
        if (stageInitializer) stageInitializer.StageInitialized += OnStageInitialized;
        ApplyStage(stageInitializer ? stageInitializer.ActiveStageMetaData : StageSelectionManager.SelectedStageMetaData);
    }
    private void OnStageInitialized(StageMetaData stage, GameObject root) => ApplyStage(stage);
    private void ApplyStage(StageMetaData stage)
    {
        AudioClip theme = stage && stage.StageId == "vandalism_block"
            ? Resources.Load<AudioClip>("Music/vandalism_fps_loop") : null;
        audioSource.Stop();
        stageLoop = theme != null;
        audioSource.loop = stageLoop;
        audioSource.volume = stageLoop ? .18f : originalVolume;
        audioSource.spatialBlend = stageLoop ? 0f : originalSpatialBlend;
        if (theme) {
            audioSource.spatialBlend = 0f;
            audioSource.clip = theme;
            audioSource.Play();
        } else PlayCurrentBGM();
    }
    void Update()
    {
        if (!stageLoop && audioSource && !audioSource.isPlaying && bgmClips != null && bgmClips.Count > 0) {
            bgmClipsIndex = (bgmClipsIndex + 1) % bgmClips.Count;
            PlayCurrentBGM();
        }
    }
    private void PlayCurrentBGM()
    {
        if (bgmClips == null || bgmClips.Count == 0) return;
        bgmClipsIndex = Mathf.Clamp(bgmClipsIndex, 0, bgmClips.Count - 1);
        audioSource.clip = bgmClips[bgmClipsIndex];
        if (audioSource.clip) audioSource.Play();
    }
    private void OnDestroy()
    {
        if (stageInitializer) stageInitializer.StageInitialized -= OnStageInitialized;
    }
}
