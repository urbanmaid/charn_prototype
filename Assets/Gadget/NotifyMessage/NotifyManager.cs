using System.Collections.Generic;
using UnityEngine;

public class NotifyManager : MonoBehaviour
{
    [Header("References")]
    public NotifyMessage prefab;
    public RectTransform parent;

    [Header("Layout")]
    public Vector2 topPosition = new Vector2(-50, 0);
    public float verticalSpacing = 80f;

    List<NotifyMessage> activeMessages = new List<NotifyMessage>();

    public void ShowMessage(string message, Sprite portraitSprite = null, string name = null)
    {
        NotifyMessage msg =
            Instantiate(prefab, parent);

        // Set relative positions by messege count
        for (int i = 0; i < activeMessages.Count; i++)
        {
            Vector2 newPos =
                topPosition +
                Vector2.down *
                verticalSpacing * (i + 1);

            activeMessages[i].SetSlotPosition(newPos);
        }

        // Set init spawn position
        Vector2 spawnPos =
            topPosition +
            new Vector2(700, 0);

        // Insert messeges
        activeMessages.Insert(0, msg);
        msg.Init(
            this,
            spawnPos,
            topPosition,
            portraitSprite,
            name,
            message
        );
    }


    public void RemoveMessage(NotifyMessage msg)
    {
        if (activeMessages.Contains(msg))
            activeMessages.Remove(msg);

        RearrangeMessages();
    }

    void RearrangeMessages()
    {
        for (int i = 0; i < activeMessages.Count; i++)
        {
            Vector2 pos =
                topPosition +
                Vector2.down *
                verticalSpacing * i;

            activeMessages[i]
                .SetSlotPosition(pos);
        }
    }
}