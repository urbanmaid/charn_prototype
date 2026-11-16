using System;
using UnityEngine;

public enum CommandType
{
    Move,
    Attack,
    Retreat,
    Hold,
    FeedBack
}

public class CommandData
{
    public CommandType Type;
    public Transform Target;
    public Vector3 Position;

    // 선택된 유닛만 명령 받게 하기 위한 필드
    public String TargetUnitName;
}

public class CommandManager : MonoBehaviour
{
    public static CommandManager Instance;

    public Action<CommandData> OnCommandIssued;
    //public PlayerCommander Player;

    public static void EnsureActiveManager()
    {
        if (Instance != null && Instance.isActiveAndEnabled) return;
        var existing = FindAnyObjectByType<CommandManager>();
        if (existing != null) { Instance = existing; return; }
        new GameObject(nameof(CommandManager)).AddComponent<CommandManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Awake()
    {
        Instance = this;
    }

    //플레이어가 아군에게
    public void IssueCommand(CommandData command)
    {
        OnCommandIssued?.Invoke(command);
    }

    //아군이 플레이어에게
    public void FeedBackByMessage(String text, Sprite portraitSprite = null, string name = null)
    {
        Debug.Log($"FeedBackByMessage 호출됨 : {text}");
        var notifications = FindAnyObjectByType<NotifyManager>();
        if (notifications != null && notifications.prefab != null && notifications.parent != null)
            notifications.ShowMessage(text, portraitSprite, name);
    }
}