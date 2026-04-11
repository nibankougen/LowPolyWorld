using UnityEngine;

/// <summary>
/// uGUI Canvas ルートの RectTransform を Screen.safeArea に合わせて調整する。
/// キャンバスルートの RectTransform にアタッチして使用する。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Rect _lastSafeArea;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        Apply(Screen.safeArea);
    }

    private void Update()
    {
        var safeArea = Screen.safeArea;
        if (safeArea != _lastSafeArea)
            Apply(safeArea);
    }

    private void Apply(Rect safeArea)
    {
        _lastSafeArea = safeArea;

        var anchorMin = safeArea.position;
        var anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        _rectTransform.anchorMin = anchorMin;
        _rectTransform.anchorMax = anchorMax;
    }
}
