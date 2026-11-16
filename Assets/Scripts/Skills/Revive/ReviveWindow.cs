using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Overwatch-like revive window: an ally can be revived only for a while after death.
/// Death time is tracked by a scene tracker created on demand (EnsureTracker).
/// </summary>
public static class ReviveWindow
{
    public const float Duration = 20f;
    private static ReviveWindowTracker tracker;

    public static void EnsureTracker()
    {
        if (tracker != null) return;
        tracker = Object.FindAnyObjectByType<ReviveWindowTracker>();
        if (tracker == null)
            tracker = new GameObject("[ReviveWindowTracker]").AddComponent<ReviveWindowTracker>();
    }

    /// <summary> Seconds left to revive. Negative if not dead or not tracked. </summary>
    public static float TimeLeft(HealthManager health, float window)
    {
        if (health == null || !health.dead || tracker == null) return -1f;
        if (!tracker.TryGetDeathTime(health, out float deathTime)) return Duration; // Not observed yet: treat as just died
        return Duration - (Time.time - deathTime);
    }

    public static bool IsRevivable(HealthManager health, float window)
    {
        return TimeLeft(health, window) > 0f;
    }

    public static IReadOnlyList<HealthManager> DeadAllies => tracker != null ? tracker.DeadAllies : System.Array.Empty<HealthManager>();

    // 실제 빈사 시점에 기록한다. 궁극기 장착/추적기 첫 관측 시점으로 제한 시간이 밀리지 않게 한다.
    public static void RecordDeath(HealthManager health)
    {
        if (health == null || !health.dead) return;
        EnsureTracker();
        if (!tracker.TryGetDeathTime(health, out _)) tracker.SetDeathTime(health, Time.time);
    }

    public static float? CaptureDeathTime(HealthManager health)
    {
        if (health == null || !health.dead) return null;
        RecordDeath(health);
        return tracker.TryGetDeathTime(health, out float time) ? time : Time.time;
    }

    public static void RestoreDeathTime(HealthManager health, float? time)
    {
        if (health == null) return;
        if (health.dead)
        {
            EnsureTracker();
            tracker.SetDeathTime(health, time ?? Time.time);
        }
        else tracker?.ClearDeath(health);
    }

    public static void HoldCountdown(HealthManager health, Object owner)
    {
        if (health == null || !health.dead || owner == null) return;
        RecordDeath(health);
        if (IsRevivable(health, Duration)) tracker.Hold(health, owner);
    }

    public static void ReleaseCountdown(HealthManager health, Object owner)
    {
        if (health != null) tracker?.Release(health, owner);
    }

    internal static void Unregister(ReviveWindowTracker t)
    {
        if (tracker == t) tracker = null;
    }
}

/// <summary>
/// Watches allies and records when each one died.
/// </summary>
public class ReviveWindowTracker : MonoBehaviour
{
    private readonly List<HealthManager> allies = new();
    private readonly Dictionary<HealthManager, float> deathTimes = new();
    private readonly List<HealthManager> deadAllies = new();
    private float nextRefresh;
    private sealed class HoldState
    {
        public float startedAt;
        public readonly HashSet<Object> owners = new();
    }
    private readonly Dictionary<HealthManager, HoldState> holds = new();

    public void Hold(HealthManager health, Object owner)
    {
        if (!holds.TryGetValue(health, out var state))
        {
            state = new HoldState { startedAt = Time.time };
            holds.Add(health, state);
        }
        state.owners.Add(owner);
    }

    public void Release(HealthManager health, Object owner)
    {
        if (!holds.TryGetValue(health, out var state)) return;
        state.owners.Remove(owner);
        if (state.owners.Count > 0) return;
        if (deathTimes.ContainsKey(health)) deathTimes[health] += Time.time - state.startedAt;
        holds.Remove(health);
    }

    public IReadOnlyList<HealthManager> DeadAllies => deadAllies;

    public bool TryGetDeathTime(HealthManager health, out float time)
    {
        if (!deathTimes.TryGetValue(health, out time)) return false;
        if (holds.TryGetValue(health, out var state)) time += Time.time - state.startedAt;
        return true;
    }

    public void SetDeathTime(HealthManager health, float time)
    {
        holds.Remove(health);
        deathTimes[health] = time;
        if (!allies.Contains(health)) allies.Add(health);
        if (!deadAllies.Contains(health)) deadAllies.Add(health);
    }

    public void ClearDeath(HealthManager health)
    {
        deathTimes.Remove(health);
        holds.Remove(health);
        deadAllies.Remove(health);
    }

    private void Update()
    {
        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + 1f;
            allies.Clear();
            foreach (HealthManager health in FindObjectsByType<HealthManager>(FindObjectsInactive.Include))
            {
                if (AllyTargeting.IsAlly(health))
                    allies.Add(health);
            }
        }

        deadAllies.Clear();
        foreach (HealthManager health in allies)
        {
            if (health == null) continue;

            if (health.dead)
            {
                if (!deathTimes.ContainsKey(health))
                    deathTimes[health] = Time.time;
                deadAllies.Add(health);
            }
            else
            {
                deathTimes.Remove(health);
                holds.Remove(health);
            }
        }
    }

    private void OnDestroy()
    {
        ReviveWindow.Unregister(this);
    }
}
