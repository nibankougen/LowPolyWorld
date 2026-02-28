using UnityEngine;
using UnityEngine.UIElements;

public class MenuOverlayController : MonoBehaviour
{
    private UIDocument _document;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        var menuButton = _document.rootVisualElement.Q<Button>("menu-button");
        menuButton.clicked += OnMenuButtonClicked;
    }

    private void OnDisable()
    {
        var root = _document.rootVisualElement;
        if (root == null)
            return;
        var menuButton = root.Q<Button>("menu-button");
        if (menuButton != null)
            menuButton.clicked -= OnMenuButtonClicked;
    }

    private void OnMenuButtonClicked()
    {
        Debug.Log("Menu button clicked");
    }
}
