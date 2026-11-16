using UnityEngine;
using UnityEngine.UI;

public class ContorlsImgRotate : MonoBehaviour
{
    public Image targetImage;   // 바뀔 UI 이미지
    public Sprite[] images;     // 순환할 이미지들

    private int currentIndex = 0;

    void Start()
    {
        UpdateImage();
    }

    // 다음 버튼
    public void NextImage()
    {
        currentIndex++;

        if (currentIndex >= images.Length)
            currentIndex = 0; // 마지막이면 처음으로

        UpdateImage();
    }

    // 이전 버튼
    public void PrevImage()
    {
        currentIndex--;

        if (currentIndex < 0)
            currentIndex = images.Length - 1; // 처음이면 마지막으로

        UpdateImage();
    }

    void UpdateImage()
    {
        targetImage.sprite = images[currentIndex];

        RectTransform rect =
            targetImage.GetComponent<RectTransform>();

        // 화면 전체 Stretch
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        targetImage.preserveAspect = true;
    }
}