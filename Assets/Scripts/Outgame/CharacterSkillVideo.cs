using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class CharacterSkillVideo : MonoBehaviour
{
    VideoPlayer player;
    RenderTexture texture;
    Text status;
    RawImage image;
    public void Initialize(string key, int slot, RawImage display, Text message)
    {
        image = display; status = message; image.enabled = false;
        string skillKey = slot == 0 ? "Q" : slot == 1 ? "E" : "X";
        var clip = Resources.Load<VideoClip>("CharacterProfiles/Videos/" + key + "_" + skillKey);
        if (!clip) clip = Resources.Load<VideoClip>("CharacterProfiles/Videos/" + key + "_" + (slot + 1));
        if (!clip) { status.text = skillKey + " 스킬 영상이 아직 등록되지 않았습니다."; return; }
        texture = new RenderTexture(1280, 720, 0); texture.Create(); image.texture = texture;
        player = gameObject.AddComponent<VideoPlayer>(); player.playOnAwake = false;
        player.source = VideoSource.VideoClip; player.clip = clip; player.isLooping = true;
        player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = texture;
        player.aspectRatio = VideoAspectRatio.FitInside;
        var audio = gameObject.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0; audio.volume = .65f;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        if (clip.audioTrackCount > 0) { player.controlledAudioTrackCount = 1; player.SetTargetAudioSource(0, audio); }
        player.prepareCompleted += Prepared; player.errorReceived += Failed;
        player.Prepare();
    }
    void Prepared(VideoPlayer source) { image.enabled = true; status.text = ""; source.Play(); }
    void Failed(VideoPlayer source, string error) { image.enabled = false; status.text = "영상을 재생하지 못했습니다."; Debug.LogWarning(error, this); }
    public void Toggle() { if (!player || !player.isPrepared) return; if (player.isPlaying) player.Pause(); else player.Play(); }
    public void Replay() { if (!player || !player.isPrepared) return; player.time = 0; player.Play(); }
    void OnDisable() { if (player) player.Stop(); }
    void OnDestroy()
    {
        if (player) { player.prepareCompleted -= Prepared; player.errorReceived -= Failed; player.targetTexture = null; }
        if (texture) { texture.Release(); Destroy(texture); }
    }
}
