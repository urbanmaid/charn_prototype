using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Fiona "Hawk's Ambush" (game changer): wait, then fire to strike every enemy visible in front.
/// Damage grows from min to max during chargeTime and stays at max afterwards.
/// If nothing is fired until maxWait, the ambush ends and part of the game changer point is refunded.
/// Player fires with the attack button (normal shooting is blocked meanwhile); AI strikes automatically.
/// </summary>
public class HawkAmbush : MonoBehaviour
{
    [Serializable]
    public struct Settings
    {
        public float minDamage, maxDamage, chargeTime, maxWait, refundRatio, range, aiStrikeTime;
        public bool requireLineOfSight;
    }

    public bool IsActive { get; private set; }

    private Settings settings;
    private SkillBehavior owner;
    private int pointBefore;
    private float elapsed;
    private bool isPlayer;
    private HealthManager casterHealth;
    private Charm.Mechanism.Missions.CombatSource combatSource;
    private ShootBehaviour shoot;
    private InputAction fireAction;
    private Camera viewCamera;
    private SkillVfxRunner readyVfx, windVfx;
    // Huge hawk flying in from behind while charging; it sends one hawk per target at the strike
    private ShadowHawk shadowHawk;
    private AudioSource chargeSound;
    private static float lastImpactSoundTime = -1f;

    private readonly List<HealthManager> candidates = new();
    private readonly List<HealthManager> targets = new();
    private float nextCandidateRefresh;

    // UI
    private GameObject canvasObject;
    private Image vignette, chargeFill, waitFill;
    private Text damageText, hintText;
    private readonly List<RectTransform> reticles = new();
    private static Sprite reticleSprite, vignetteSprite, whiteSprite;

    public float CurrentDamage => Mathf.Lerp(settings.minDamage, settings.maxDamage, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, settings.chargeTime)));

    public bool Begin(Settings settings, SkillBehavior owner, int pointBefore)
    {
        if (IsActive) return false;

        this.settings = settings;
        this.owner = owner;
        this.pointBefore = pointBefore;
        elapsed = 0f;
        IsActive = true;
        isPlayer = CompareTag("Player");
        casterHealth = GetComponent<HealthManager>();
        combatSource = Charm.Mechanism.Missions.CombatTelemetry.Capture(gameObject);

        if (isPlayer)
        {
            shoot = GetComponent<ShootBehaviour>();
            if (shoot != null) shoot.AddShootBlock();
            PlayerInput input = GetComponent<PlayerInput>();
            fireAction = input != null ? input.actions.FindAction("Attack") : null;
            BasicBehaviour basic = GetComponent<BasicBehaviour>();
            viewCamera = basic != null && basic.playerCamera != null ? basic.playerCamera.GetComponent<Camera>() : Camera.main;
            BuildUI();
        }

        FionaVfx.SpawnHawkActivate(transform);
        readyVfx = FionaVfx.SpawnHawkReady(transform);
        windVfx = FionaVfx.SpawnHawkWindSwirl(transform);
        Vector3 approach = viewCamera != null ? viewCamera.transform.forward : transform.forward;
        shadowHawk = ShadowHawk.Begin(transform, approach, isPlayer);
        // Heavy riser that peaks when damage reaches max
        chargeSound = SkillSfx.PlayAttached("hawk_charge", transform, 0.9f);
        return true;
    }

    private void Update()
    {
        if (!IsActive) return;

        if (casterHealth != null && casterHealth.dead)
        {
            End();
            return;
        }

        elapsed += Time.deltaTime;
        CollectTargets();

        if (isPlayer)
        {
            UpdateUI();
            if (fireAction != null && fireAction.WasPressedThisFrame())
            {
                if (targets.Count > 0) Strike();
                else
                {
                    SkillToast.Show("NO TARGET IN SIGHT", FionaVfx.HawkAmber);
                    SkillSfx.Play("skill_error", transform.position, 0.6f);
                }
                if (!IsActive) return;
            }
        }
        else if (elapsed >= settings.aiStrikeTime && targets.Count > 0)
        {
            Strike();
            return;
        }

        if (elapsed >= settings.maxWait)
            Expire();
    }

    #region Targets
    private void CollectTargets()
    {
        if (Time.time >= nextCandidateRefresh)
        {
            nextCandidateRefresh = Time.time + 0.4f;
            candidates.Clear();
            foreach (HealthManager health in FindObjectsByType<HealthManager>(FindObjectsInactive.Exclude))
            {
                if ((health is EnemyAI.EnemyHealth || health is TargetHealth) && !AllyTargeting.IsAlly(health))
                    candidates.Add(health);
            }
        }

        targets.Clear();
        Transform view = viewCamera != null ? viewCamera.transform : transform;
        foreach (HealthManager enemy in candidates)
        {
            if (enemy == null || enemy.dead || !enemy.isActiveAndEnabled) continue;

            Vector3 aim = GetAimPoint(enemy);
            if ((aim - transform.position).sqrMagnitude > settings.range * settings.range) continue;

            // In front: on screen for player, 100 degree cone for AI
            if (viewCamera != null)
            {
                Vector3 vp = viewCamera.WorldToViewportPoint(aim);
                if (vp.z <= 0f || vp.x < 0.02f || vp.x > 0.98f || vp.y < 0.02f || vp.y > 0.98f) continue;
            }
            else if (Vector3.Angle(transform.forward, aim - transform.position) > 50f) continue;

            if (settings.requireLineOfSight && !AllyTargeting.HasLineOfSight(view.position, aim, transform.root, enemy.transform.root))
                continue;

            targets.Add(enemy);
        }
    }

    private static Vector3 GetAimPoint(HealthManager enemy)
    {
        if (enemy.GetComponent<EnemyAI.StateController>() != null)
            return EnemyAI.StateController.GetAimPoint(enemy.transform);
        return enemy.transform.position + Vector3.up * 1.2f;
    }
    #endregion

    #region Strike / Expire
    // Release one hawk per target; damage is dealt when each hawk lands
    private void Strike()
    {
        float damage = CurrentDamage;
        Vector3 shooterPoint = shoot != null && shoot.GunMuzzle != null ? shoot.GunMuzzle.position : transform.position + Vector3.up * 1.4f;
        GameObject attacker = gameObject;
        bool showNumbers = isPlayer;
        Color numberColor = damage >= settings.maxDamage ? new Color(1f, 0.35f, 0.2f) : FionaVfx.HawkAmber;

        FionaVfx.SpawnHawkLaunch(shooterPoint);
        if (isPlayer && viewCamera != null)
        {
            var orbit = viewCamera.GetComponent<ThirdPersonOrbitCam>();
            var feedback = CombatCameraFeedback.Get(orbit);
            if (feedback) feedback.SkillPulse(1.1f, .6f, 32f, .65f);
        }
        // Sonic boom first, then the hawk cry
        SkillSfx.Play("hawk_launch", shooterPoint, 0.9f);
        SkillSfx.Play("hawk_cry", shooterPoint, 0.8f, 1f, 0f, 0.3f);

        // Gusts rush forward on both sides as the hawks leave
        Vector3 forward = targets.Count > 0 ? GetAimPoint(targets[0]) - transform.position : transform.forward;
        FionaVfx.SpawnHawkWindGust(transform, forward);

        // The huge hawk overhead flashes and sends one hawk down onto each target
        Vector3 skyPoint = shadowHawk != null ? shadowHawk.Position : transform.position + Vector3.up * 15f;
        if (shadowHawk != null) shadowHawk.Release();
        shadowHawk = null;

        var strikeSource = combatSource;
        int index = 0;
        foreach (HealthManager enemy in targets)
        {
            if (enemy == null || enemy.dead) continue;
            HealthManager victim = enemy;
            Vector3 aim = GetAimPoint(enemy);
            Vector3 spread = new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-2f, 2f));

            DiveHawk.Launch(skyPoint + spread, enemy.transform, aim - enemy.transform.position, index * 0.08f, point =>
            {
                if (victim != null && !victim.dead)
                {
                    var rewardOwner = attacker != null ? attacker.GetComponent<SkillBehavior>() : null;
                    System.Action hit = () =>
                    {
                        using (Charm.Mechanism.Missions.CombatTelemetry.UseSource(strikeSource))
                            victim.TakeDamage(point, Vector3.down, damage, null, attacker, HealthManager.DamageType.Skill);
                    };
                    if (rewardOwner != null) rewardOwner.WithoutGameChangerReward(hit);
                    else hit();
                }
                FionaVfx.SpawnHawkExplosion(point);
                // Hawks land close together: avoid stacking booms
                if (Time.time - lastImpactSoundTime > 0.15f)
                {
                    lastImpactSoundTime = Time.time;
                    SkillSfx.Play("hawk_impact", point, 0.95f, .86f);
                }
                if (showNumbers) SkillScreenFx.DamageNumber(point, damage, numberColor);
            });
            index++;
        }
        if (isPlayer) SkillScreenFx.Flash(new Color(1f, 0.8f, 0.4f, 0.25f), 0.25f);
        End();
    }

    private void Expire()
    {
        int refund = Mathf.RoundToInt(pointBefore * settings.refundRatio);
        if (owner != null) owner.gameChangerPoint = Mathf.Max(owner.gameChangerPoint, refund);
        if (isPlayer) SkillToast.Show($"AMBUSH EXPIRED  -  {Mathf.RoundToInt(settings.refundRatio * 100f)}% REFUNDED", FionaVfx.HawkAmber);
        FionaVfx.SpawnHawkExpire(transform);
        End();
    }

    private void End()
    {
        IsActive = false;
        if (shoot != null) shoot.RemoveShootBlock();
        shoot = null;
        fireAction = null;
        if (readyVfx != null) readyVfx.StopSoon(0.6f);
        if (windVfx != null) windVfx.StopSoon(0.6f);
        windVfx = null;
        if (shadowHawk != null) shadowHawk.Leave();
        shadowHawk = null;
        SkillSfx.Stop(chargeSound, 0.15f);
        chargeSound = null;
        readyVfx = null;
        if (canvasObject != null) Destroy(canvasObject);
        reticles.Clear();
    }

    private void OnDisable()
    {
        if (IsActive) End();
    }
    #endregion

    #region UI
    private void BuildUI()
    {
        canvasObject = new GameObject("HawkAmbushUI");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;

        // Amber vignette on screen edges
        vignette = NewImage(canvasObject.transform, "Vignette", VignetteSprite);
        Stretch(vignette.rectTransform);
        vignette.color = new Color(1f, 0.6f, 0.15f, 0f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        RectTransform panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvasObject.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.27f);
        panel.sizeDelta = new Vector2(300f, 80f);

        if (font != null)
        {
            Text title = NewText(panel, font, "HAWK'S AMBUSH", 15, new Vector2(0f, 34f), FionaVfx.HawkAmber);
            title.fontStyle = FontStyle.Bold;
            damageText = NewText(panel, font, "", 30, new Vector2(0f, 8f), Color.white);
            damageText.fontStyle = FontStyle.Bold;
            hintText = NewText(panel, font, "[LMB]  STRIKE", 13, new Vector2(0f, -40f), new Color(1f, 1f, 1f, 0.85f));
        }

        chargeFill = NewBar(panel, new Vector2(0f, -14f), new Vector2(260f, 8f), FionaVfx.HawkAmber);
        waitFill = NewBar(panel, new Vector2(0f, -25f), new Vector2(260f, 4f), new Color(1f, 1f, 1f, 0.6f));
    }

    private void UpdateUI()
    {
        if (canvasObject == null) return;

        float charge = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, settings.chargeTime));
        bool full = charge >= 1f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (full ? 10f : 4f));

        vignette.color = new Color(1f, 0.6f, 0.15f, Mathf.Lerp(0.25f, 0.45f, charge) + 0.08f * pulse);
        chargeFill.fillAmount = charge;
        chargeFill.color = full ? Color.Lerp(FionaVfx.HawkAmber, new Color(1f, 0.35f, 0.2f), pulse) : FionaVfx.HawkAmber;
        waitFill.fillAmount = 1f - Mathf.Clamp01(elapsed / settings.maxWait);
        if (damageText) damageText.text = full ? $"{Mathf.RoundToInt(CurrentDamage)}  MAX" : Mathf.RoundToInt(CurrentDamage).ToString();
        if (hintText) hintText.text = targets.Count > 0 ? $"[LMB]  STRIKE  x{targets.Count}" : "NO TARGET IN SIGHT";

        // Reticle on each target: tightens while charging, turns red at max
        int used = 0;
        if (viewCamera != null)
        {
            foreach (HealthManager enemy in targets)
            {
                Vector3 screen = viewCamera.WorldToScreenPoint(GetAimPoint(enemy));
                if (screen.z <= 0f) continue;
                RectTransform reticle = GetReticle(used++);
                reticle.gameObject.SetActive(true);
                reticle.anchoredPosition = screen;
                float size = Mathf.Lerp(150f, 70f, charge) * (full ? 1f + 0.06f * pulse : 1f);
                reticle.sizeDelta = new Vector2(size, size);
                reticle.localRotation = Quaternion.Euler(0f, 0f, (1f - charge) * 90f);
                reticle.GetComponent<Image>().color = full ? new Color(1f, 0.35f, 0.2f, 0.95f) : new Color(1f, 0.75f, 0.3f, 0.85f);
            }
        }
        for (int i = used; i < reticles.Count; i++)
            reticles[i].gameObject.SetActive(false);
    }

    private RectTransform GetReticle(int index)
    {
        while (reticles.Count <= index)
        {
            Image image = NewImage(canvasObject.transform, "Reticle", ReticleSprite);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
            reticles.Add(image.rectTransform);
        }
        return reticles[index];
    }

    private static Image NewImage(Transform parent, string name, Sprite sprite)
    {
        Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static Text NewText(Transform parent, Font font, string value, int size, Vector2 position, Color color)
    {
        Text text = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        text.transform.SetParent(parent, false);
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        text.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = new Vector2(300f, 36f);
        return text;
    }

    private static Image NewBar(Transform parent, Vector2 position, Vector2 size, Color color)
    {
        Image back = NewImage(parent, "BarBack", null);
        back.color = new Color(0f, 0f, 0f, 0.5f);
        back.rectTransform.anchoredPosition = position;
        back.rectTransform.sizeDelta = size;

        Image fill = NewImage(back.transform, "Fill", WhiteSprite);
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.color = color;
        return fill;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Sprite ReticleSprite
    {
        get
        {
            if (reticleSprite != null) return reticleSprite;
            Texture2D tex = SkillVfxFactory.Kenney("magic_03");
            reticleSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return reticleSprite;
        }
    }

    private static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite != null) return whiteSprite;
            Texture2D white = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f));
            return whiteSprite;
        }
    }

    // Transparent center, opaque edges
    private static Sprite VignetteSprite
    {
        get
        {
            if (vignetteSprite != null) return vignetteSprite;
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half, dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, r))));
                }
            tex.Apply();
            vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return vignetteSprite;
        }
    }
    #endregion
}
