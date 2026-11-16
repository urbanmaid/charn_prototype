using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NotifyMessage : MonoBehaviour
{
    RectTransform rect;
    NotifyManager manager;

    Vector2 currentTarget;

    public float moveSpeed = 10f;
    public float stayTime = 2f;

    bool exiting = false;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    void Update()
    {
        rect.anchoredPosition =
            Vector2.Lerp(
                rect.anchoredPosition,
                currentTarget,
                Time.deltaTime * moveSpeed
            );
    }

    public void Init(
        NotifyManager owner,
        Vector2 spawnPos,
        Vector2 targetPos,
        Sprite portraitSprite,
        string name,
        string text)
    {
        manager = owner;

        Transform portraitObj = transform.Find("Portrait");
        Image portraitImg = portraitObj.GetComponent<Image>();
        portraitImg.sprite = portraitSprite;

        Transform nameObj = transform.Find("Name");
        TextMeshProUGUI nameText = nameObj.GetComponent<TextMeshProUGUI>();
        nameText.text = name;

        Transform messageObj = transform.Find("Message");
        TextMeshProUGUI textComp = messageObj.GetComponent<TextMeshProUGUI>();
        textComp.text = text;

        rect.anchoredPosition = spawnPos;
        currentTarget = targetPos;

        StartCoroutine(NotifyRoutine());
    }

    IEnumerator NotifyRoutine()
    {
        yield return new WaitForSeconds(stayTime);

        exiting = true;

        currentTarget += new Vector2(700, 0);

        yield return new WaitUntil(
            () => Vector2.Distance(
                rect.anchoredPosition,
                currentTarget) < 2f
        );

        manager.RemoveMessage(this);

        Destroy(gameObject);
    }

    public void SetSlotPosition(Vector2 newPos)
    {
        if (exiting) return;
        currentTarget = newPos;
    }
}