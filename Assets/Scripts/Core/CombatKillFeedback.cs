using UnityEngine;
using UnityEngine.UI;

// Attached only to the local player's damage origin; one marker for simultaneous kills.
public sealed class CombatKillFeedback : MonoBehaviour
{
    private static AudioClip headshotClip;
    private static bool headshotClipLoaded;
    private static AudioClip hitConfirmClip;
    private static bool hitConfirmClipLoaded;
    private static AudioClip shieldHitClip;
    private static bool shieldHitClipLoaded;
    private static AudioClip shieldBreakClip;
    private static bool shieldBreakClipLoaded;

    private float remaining;
    private float markerStartedAt;
    private bool headshot;
    private bool kill, shield, shieldBreak;
    private float nextHeadshotSoundAt;
    private float nextHitSoundAt;
    private float nextShieldHitSoundAt;
    private AudioSource headshotAudio;
    private AudioSource killAudio;
    private static AudioClip killClip;
    private float nextKillSound;

    private void PlayKillSound(bool critical)
    {
        if (Time.unscaledTime < nextKillSound) return;
        nextKillSound = Time.unscaledTime + .12f;
        if (!killClip) {
            const int rate = 22050;
            var samples = new float[5292];
            for (int i = 0; i < samples.Length; i++) {
                float t = i / (float)rate;
                float envelope = Mathf.Clamp01(t / .003f) * Mathf.Exp(-t * 19f) * Mathf.Clamp01((.24f - t) / .025f);
                samples[i] = (Mathf.Sin(2f * Mathf.PI * 520f * t) * .35f +
                    Mathf.Sin(2f * Mathf.PI * 780f * t) * .2f) * envelope;
            }
            killClip = AudioClip.Create("Combat kill resolve", samples.Length, 1, rate, false);
            killClip.SetData(samples, 0);
        }
        if (!killAudio) {
            killAudio = gameObject.AddComponent<AudioSource>();
            killAudio.playOnAwake = false; killAudio.spatialBlend = 0f;
            killAudio.priority = 30; killAudio.outputAudioMixerGroup = SkillSfx.MixerGroup;
        }
        killAudio.Stop();
        killAudio.clip = killClip;
        killAudio.pitch = critical ? 1.2f : 1f;
        killAudio.volume = .3f;
        killAudio.Play();
    }

    public static void Show(GameObject origin, bool wasHeadshot)
    {
        if (!origin) return;
        var player = origin.GetComponentInParent<PlayerHealth>();
        if (!player || player.dead) return;
        var feedback = player.GetComponent<CombatKillFeedback>();
        if (!feedback) feedback = player.gameObject.AddComponent<CombatKillFeedback>();
        feedback.kill = true;
        feedback.shield = false;
        feedback.shieldBreak = false;
        feedback.headshot = wasHeadshot;
        feedback.remaining = 0.32f;
        feedback.markerStartedAt = Time.unscaledTime;
        feedback.PlayKillSound(wasHeadshot);
    }

    public static void Confirm(GameObject origin, HealthManager target, float healthLost, float shieldLost, bool killed = false, bool wasHeadshot = false, bool shieldBroken = false)
    {
        if (!origin || !target || (healthLost <= 0f && shieldLost <= 0f)) return;
        var player = origin.GetComponentInParent<PlayerHealth>();
        if (!player || player.dead || target == player) return;
        if (TeamCodeUtility.TryGetTeamCode(player, out int ownTeam) &&
            TeamCodeUtility.TryGetTeamCode(target, out int targetTeam) && ownTeam == targetTeam) return;
        var shooter = origin.GetComponentInParent<ShootBehaviour>();
        if (shooter != null) CombatWeaponAudio.On(shooter.gameObject).ConfirmHit(wasHeadshot);
        var feedback = player.GetComponent<CombatKillFeedback>();
        if (!feedback) feedback = player.gameObject.AddComponent<CombatKillFeedback>();
        if (wasHeadshot) feedback.PlayHeadshotSound();
        else if (shieldBroken) feedback.PlayShieldSound(true);
        else if (shieldLost > 0f && healthLost <= 0f) feedback.PlayShieldSound(false);
        else if (healthLost > 0f) feedback.PlayHitConfirmSound();
        if (killed) { Show(origin, wasHeadshot); return; }
        if (feedback.kill && feedback.remaining > 0f) return;
        // A body pellet or rapid follow-up shot must not erase a fresh headshot marker.
        if (feedback.headshot && feedback.remaining > 0f && !wasHeadshot) return;
        feedback.kill = false;
        feedback.headshot = wasHeadshot;
        feedback.shield = !wasHeadshot && shieldLost > 0f;
        feedback.shieldBreak = !wasHeadshot && shieldBroken;
        feedback.remaining = wasHeadshot ? 0.28f : shieldBroken ? 0.20f : 0.14f;
        feedback.markerStartedAt = Time.unscaledTime;
    }

    private void PlayShieldSound(bool broken)
    {
        if (!broken && Time.unscaledTime < nextShieldHitSoundAt) return;
        AudioClip clip;
        if (broken)
        {
            if (!shieldBreakClipLoaded)
            {
                shieldBreakClip = Resources.Load<AudioClip>("SFX/shield_break_confirm");
                shieldBreakClipLoaded = true;
            }
            clip = shieldBreakClip;
        }
        else
        {
            if (!shieldHitClipLoaded)
            {
                shieldHitClip = Resources.Load<AudioClip>("SFX/shield_hit_confirm");
                shieldHitClipLoaded = true;
            }
            clip = shieldHitClip;
        }
        if (!clip) return;
        if (!headshotAudio)
        {
            headshotAudio = gameObject.AddComponent<AudioSource>();
            headshotAudio.playOnAwake = false;
            headshotAudio.loop = false;
            headshotAudio.spatialBlend = 0f;
            headshotAudio.priority = 48;
            headshotAudio.outputAudioMixerGroup = SkillSfx.MixerGroup;
        }
        headshotAudio.PlayOneShot(clip, broken ? 0.72f : 0.45f);
        if (!broken) nextShieldHitSoundAt = Time.unscaledTime + 0.08f;
    }

    private void PlayHitConfirmSound()
    {
        if (Time.unscaledTime < nextHitSoundAt) return;
        if (!hitConfirmClipLoaded)
        {
            hitConfirmClip = Resources.Load<AudioClip>("SFX/hit_confirm");
            hitConfirmClipLoaded = true;
        }
        if (!hitConfirmClip) return;

        if (!headshotAudio)
        {
            headshotAudio = gameObject.AddComponent<AudioSource>();
            headshotAudio.playOnAwake = false;
            headshotAudio.loop = false;
            headshotAudio.spatialBlend = 0f;
            headshotAudio.priority = 48;
            headshotAudio.outputAudioMixerGroup = SkillSfx.MixerGroup;
        }

        headshotAudio.PlayOneShot(hitConfirmClip, 0.9f);
        nextHitSoundAt = Time.unscaledTime + 0.06f;
    }

    private void PlayHeadshotSound()
    {
        if (Time.unscaledTime < nextHeadshotSoundAt) return;
        if (!headshotClipLoaded)
        {
            headshotClip = Resources.Load<AudioClip>("SFX/headshot_confirm");
            headshotClipLoaded = true;
        }
        if (!headshotClip) return;

        if (!headshotAudio)
        {
            headshotAudio = gameObject.AddComponent<AudioSource>();
            headshotAudio.playOnAwake = false;
            headshotAudio.loop = false;
            headshotAudio.spatialBlend = 0f;
            headshotAudio.priority = 48;
            headshotAudio.outputAudioMixerGroup = SkillSfx.MixerGroup;
        }

        headshotAudio.priority = 24;
        headshotAudio.Stop();
        headshotAudio.PlayOneShot(headshotClip, 1f);
        nextHitSoundAt = Time.unscaledTime + .10f;
        nextHeadshotSoundAt = Time.unscaledTime + 0.06f;
    }

    private Canvas markerCanvas;
    private readonly Image[] markerLines = new Image[10];

    private void EnsureMarkerCanvas()
    {
        if (markerCanvas != null) return;
        var root = new GameObject("Combat hit confirmation HUD", typeof(RectTransform), typeof(Canvas));
        markerCanvas = root.GetComponent<Canvas>();
        markerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        markerCanvas.sortingOrder = short.MaxValue;
        // No GraphicRaycaster and no raycast targets: feedback never intercepts input.
        for (int i = 0; i < markerLines.Length; i++)
        {
            var line = new GameObject("Marker line " + i, typeof(RectTransform), typeof(Image));
            line.transform.SetParent(root.transform, false);
            var image = markerLines[i] = line.GetComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.rectTransform.pivot = new Vector2(.5f, .5f);
        }
    }

    private void Update()
    {
        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        bool visible = remaining > 0f && Time.timeScale > 0f;
        if (!visible)
        {
            if (markerCanvas != null) markerCanvas.enabled = false;
            return;
        }
        EnsureMarkerCanvas();
        markerCanvas.enabled = true;
        float scale = Mathf.Clamp(Screen.height / 1080f, .65f, 2f);
        Color color = shieldBreak ? new Color(.3f, .95f, 1f) : shield ? new Color(.4f, .8f, 1f) :
            headshot ? new Color(1f, .25f, .16f) : kill ? new Color(1f, .9f, .75f) : Color.white;
        color.a = Mathf.Clamp01(remaining / (kill ? .12f : .06f));
        float size = scale * (headshot ? 1.7f : shieldBreak ? 1.55f : kill ? 1.2f : 1.15f);
        // A brief outward snap makes each confirmed impact readable without moving the camera.
        float punch = Mathf.Exp(-(Time.unscaledTime - markerStartedAt) * 32f);
        size *= 1f + (headshot ? .42f : .24f) * punch;
        for (int i = 0; i < markerLines.Length; i++) markerLines[i].enabled = false;
        float length = (headshot ? 10f : kill ? 9f : 6.5f) * size;
        for (int i = 0; i < 4; i++)
        {
            float angle = (shield ? 0f : 45f) + i * 90f;
            float radians = angle * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (8f * size + length * .5f);
            Line(i, position, new Vector2(length, (2f + .8f * punch) * size), angle, color);
        }
        if (headshot)
        {
            Line(4, new Vector2(0f, 23.5f * size), new Vector2(8f, 3f) * size, 0f, color);
            Line(5, new Vector2(0f, -23.5f * size), new Vector2(8f, 3f) * size, 0f, color);
        }
        else if (shieldBreak)
        {
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f, radians = angle * Mathf.Deg2Rad;
                Line(4 + i, new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 17f * size,
                    new Vector2(34f, 2f) * size, angle + 90f, color);
            }
        }
    }

    private void Line(int index, Vector2 position, Vector2 size, float rotation, Color color)
    {
        var line = markerLines[index];
        line.enabled = true;
        line.color = color;
        line.rectTransform.anchoredPosition = position;
        line.rectTransform.sizeDelta = size;
        line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }

    private void OnDisable()
    {
        remaining = 0f;
        if (markerCanvas != null) markerCanvas.enabled = false;
    }

    private void OnDestroy()
    {
        if (markerCanvas != null) Destroy(markerCanvas.gameObject);
    }
}
