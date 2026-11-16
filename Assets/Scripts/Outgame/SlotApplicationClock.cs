using UnityEngine;
// Independent of timescale and scene lifetime. No clock is started before slot binding.
public sealed class SlotApplicationClock : MonoBehaviour
{
    static SlotApplicationClock instance;
    double nextSave;
    public static void Ensure()
    {
        if (instance) return;
        instance = new GameObject("SlotApplicationClock").AddComponent<SlotApplicationClock>();
        DontDestroyOnLoad(instance.gameObject);
    }
    void Update()
    {
        if (Time.realtimeSinceStartupAsDouble < nextSave) return;
        nextSave = Time.realtimeSinceStartupAsDouble + 30;
        StageSaveSession.FlushApplicationTime();
    }
    void OnApplicationQuit() => StageSaveSession.FlushApplicationTime();
    void OnApplicationPause(bool paused) { if (paused) StageSaveSession.FlushApplicationTime(); }
    void OnDestroy() { if (instance == this) instance = null; }
}
