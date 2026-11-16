using System;
using System.Collections.Generic;
using EnemyAI;
using UnityEngine;

public class HUDArrayFSM : MonoBehaviour
{
    [SerializeField] private AllySquadManager squadManager;
    [SerializeField] private GameObject statusUIPrefab;
    [SerializeField] private GameObject[] statusUIInstance;
    [SerializeField] private float refreshInterval = 0.1f;

    [SerializeField] private HUDArrayFSMHeader headerUI; // Object which is in first of parent object

    private readonly List<StateController> trackedControllers = new();
    private readonly List<GameObject> trackedSources = new();
    private GameObject highlightedPA;
    private bool highlightActive;
    private float nextRefreshTime;

    public void Init()
    {
        ResolveSquadManager();
        RebuildStatusUI();
        UpdateFSMInfo();
    }

    private void Start()
    {
        Init();
    }

    private void Update()
    {
        if(refreshInterval > 0f && Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, refreshInterval);
        UpdateFSMInfo();
    }

    public void UpdateFSMInfo()
    {
        ResolveSquadManager();
        if(squadManager == null || statusUIPrefab == null)
        {
            ClearStatusUI();
            return;
        }

        if(NeedsRebuild())
        {
            RebuildStatusUI();
        }

        for(int i = 0; i < trackedControllers.Count; i++)
        {
            GameObject source = i < trackedSources.Count ? trackedSources[i] : null;
            GameObject instance = statusUIInstance != null && i < statusUIInstance.Length ? statusUIInstance[i] : null;
            RefreshStatusUI(instance, source);
        }

        ApplyHighlights();
    }

    public void SetHighlightedPA(GameObject selectedPA, bool active)
    {
        highlightedPA = selectedPA;
        highlightActive = active && selectedPA != null;
        ApplyHighlights();
    }

    public void ClearHighlights()
    {
        SetHighlightedPA(null, false);
    }

    private void ResolveSquadManager()
    {
        if(squadManager == null)
        {
            squadManager = AllySquadManager.Instance;
        }
    }

    private bool NeedsRebuild()
    {
        IReadOnlyList<GameObject> allPAOnField = squadManager.AllPAOnField;
        int allyFSMCount = CountAllyFSMs(allPAOnField);
        if(allyFSMCount != trackedControllers.Count)
        {
            return true;
        }

        int trackedIndex = 0;
        for(int i = 1; i < allPAOnField.Count; i++)
        {
            GameObject pa = allPAOnField[i];
            if(pa == null)
            {
                continue;
            }

            pa.TryGetComponent(out StateController controller);
            if(trackedIndex >= trackedControllers.Count || trackedControllers[trackedIndex] != controller)
            {
                return true;
            }

            if(trackedIndex >= trackedSources.Count || trackedSources[trackedIndex] != pa)
            {
                return true;
            }

            trackedIndex++;
        }

        return false;
    }

    private void RebuildStatusUI()
    {
        ClearStatusUI();

        if(squadManager == null || statusUIPrefab == null)
        {
            return;
        }

        IReadOnlyList<GameObject> allPAOnField = squadManager.AllPAOnField;
        List<GameObject> instances = new();

        for(int i = 1; i < allPAOnField.Count; i++)
        {
            GameObject pa = allPAOnField[i];
            if(pa == null)
            {
                continue;
            }

            pa.TryGetComponent(out StateController controller);
            GameObject instance = Instantiate(statusUIPrefab, transform);
            instance.name = $"{statusUIPrefab.name}_{pa.name}";

            trackedControllers.Add(controller);
            trackedSources.Add(pa);
            BindStatusUI(instance, pa);
            instances.Add(instance);
        }

        statusUIInstance = instances.ToArray();
        ApplyHighlights();
    }

    private void ClearStatusUI()
    {
        trackedControllers.Clear();
        trackedSources.Clear();

        if(statusUIInstance == null)
        {
            statusUIInstance = new GameObject[0];
            return;
        }

        for(int i = 0; i < statusUIInstance.Length; i++)
        {
            GameObject instance = statusUIInstance[i];
            if(instance == null)
            {
                continue;
            }

            if(Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        statusUIInstance = new GameObject[0];
    }

    private void ApplyHighlights()
    {
        if(statusUIInstance == null)
        {
            return;
        }

        for(int i = 0; i < statusUIInstance.Length; i++)
        {
            GameObject instance = statusUIInstance[i];
            GameObject source = i < trackedSources.Count ? trackedSources[i] : null;
            HUDHighlight highlight = instance != null ? instance.GetComponentInChildren<HUDHighlight>(true) : null;
            if(highlight != null)
            {
                highlight.Show(highlightActive && source == highlightedPA);
            }
        }
    }

    private int CountAllyFSMs(IReadOnlyList<GameObject> allPAOnField)
    {
        int count = 0;
        for(int i = 1; i < allPAOnField.Count; i++)
        {
            GameObject pa = allPAOnField[i];
            if(pa != null)
            {
                count++;
            }
        }

        return count;
    }

    private void BindStatusUI(GameObject instance, GameObject source)
    {
        if(instance == null || source == null)
        {
            return;
        }

        HUDPersona hudPersona = instance.GetComponentInChildren<HUDPersona>(true);
        if(hudPersona != null)
        {
            PersonaBehavior personaBehavior = source.GetComponent<PersonaBehavior>();
            if(personaBehavior != null)
            {
                hudPersona.Init(personaBehavior);
            }
            hudPersona.SetDeadVisual(IsDead(source));
        }

        HUDHealth hudHealth = instance.GetComponentInChildren<HUDHealth>(true);
        if(hudHealth != null)
        {
            hudHealth.Init(source.GetComponent<HealthManager>());
        }

        HUDIngameEffects hudIngameEffects = instance.GetComponentInChildren<HUDIngameEffects>(true);
        if(hudIngameEffects != null)
        {
            IngameEffectBehavior ingameEffectBehavior = source.GetComponent<IngameEffectBehavior>();
            if(ingameEffectBehavior != null)
            {
                hudIngameEffects.Init(ingameEffectBehavior);
                hudIngameEffects.RefreshRuntimeEffects();
            }
        }

        HUDSkills hudSkills = instance.GetComponentInChildren<HUDSkills>(true);
        if(hudSkills != null)
        {
            SkillBehavior skillBehavior = source.GetComponent<SkillBehavior>();
            if(skillBehavior != null)
            {
                hudSkills.Init(skillBehavior);
            }
        }
    }

    private void RefreshStatusUI(GameObject instance, GameObject source)
    {
        if(instance == null || source == null)
        {
            return;
        }

        HUDPersona hudPersona = instance.GetComponentInChildren<HUDPersona>(true);
        if(hudPersona != null)
        {
            PersonaBehavior personaBehavior = source.GetComponent<PersonaBehavior>();
            if(personaBehavior != null)
            {
                hudPersona.Init(personaBehavior);
            }
            hudPersona.SetDeadVisual(IsDead(source));
        }

        HUDIngameEffects hudIngameEffects = instance.GetComponentInChildren<HUDIngameEffects>(true);
        if(hudIngameEffects != null)
        {
            IngameEffectBehavior ingameEffectBehavior = source.GetComponent<IngameEffectBehavior>();
            if(ingameEffectBehavior != null)
            {
                hudIngameEffects.Init(ingameEffectBehavior);
                hudIngameEffects.RefreshRuntimeEffects();
            }
        }

        HUDHealth hudHealth = instance.GetComponentInChildren<HUDHealth>(true);
        if(hudHealth != null)
        {
            hudHealth.Init(source.GetComponent<HealthManager>());
        }

        HUDSkills hudSkills = instance.GetComponentInChildren<HUDSkills>(true);
        if(hudSkills != null)
        {
            SkillBehavior skillBehavior = source.GetComponent<SkillBehavior>();
            if(skillBehavior != null)
            {
                hudSkills.Init(skillBehavior);
            }
        }
    }

    internal void SetSwapMode(bool value)
    {
        if(headerUI)
        {
            headerUI.SwapMode(value);
        }
    }

    private bool IsDead(GameObject source)
    {
        HealthManager healthManager = source != null ? source.GetComponent<HealthManager>() : null;
        return healthManager != null && healthManager.dead;
    }
}
