using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndGamePanelUI : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI messageText;
    private TextMeshProUGUI hintText;
    private Button mainMenuButton;

    private void Awake()
    {
        BuildIfNeeded();
        Hide();
    }

    public void ShowResult(string title, string message, bool showMainMenuButton, string buttonLabel = "MAIN MENU")
    {
        BuildIfNeeded();

        titleText.text = title;
        messageText.text = message;
        hintText.text = showMainMenuButton ? "Ready to head back and choose another mode?" : "Next stage starts in a moment...";
        mainMenuButton.gameObject.SetActive(showMainMenuButton);
        mainMenuButton.GetComponentInChildren<TextMeshProUGUI>().text = buttonLabel;

        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = showMainMenuButton;
        canvasGroup.blocksRaycasts = true;
    }

    public void Hide()
    {
        BuildIfNeeded();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    private void BuildIfNeeded()
    {
        if (canvasGroup != null)
            return;

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        RectTransform root = CreateRect("End Game Backdrop", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.one);
        Image backdrop = root.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.02f, 0.035f, 0.055f, 0.72f);

        RectTransform card = CreateRect("Result Card", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 430f));
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.08f, 0.12f, 0.15f, 0.96f);
        Outline cardOutline = card.gameObject.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        cardOutline.effectDistance = new Vector2(6f, -6f);

        RectTransform header = CreateRect("Header Ribbon", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -18f), new Vector2(-46f, 96f));
        Image headerImage = header.gameObject.AddComponent<Image>();
        headerImage.color = new Color(0.09f, 0.64f, 0.92f, 1f);
        Outline headerOutline = header.gameObject.AddComponent<Outline>();
        headerOutline.effectColor = new Color(0.02f, 0.05f, 0.08f, 1f);
        headerOutline.effectDistance = new Vector2(4f, -4f);

        titleText = CreateText("Title", header, "", 68f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        AddTextShadow(titleText.gameObject, new Color(0f, 0f, 0f, 0.7f), new Vector2(4f, -4f));

        RectTransform messageRect = CreateRect("Message", card, new Vector2(0.07f, 0.38f), new Vector2(0.93f, 0.68f), Vector2.zero, Vector2.zero);
        messageText = CreateText("Message Text", messageRect, "", 42f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        messageText.enableAutoSizing = true;
        messageText.fontSizeMin = 24f;
        messageText.fontSizeMax = 42f;
        AddTextShadow(messageText.gameObject, new Color(0f, 0f, 0f, 0.65f), new Vector2(3f, -3f));

        RectTransform hintRect = CreateRect("Hint", card, new Vector2(0.1f, 0.28f), new Vector2(0.9f, 0.38f), Vector2.zero, Vector2.zero);
        hintText = CreateText("Hint Text", hintRect, "", 24f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.78f, 0.92f, 1f, 1f));

        RectTransform buttonRect = CreateRect("Main Menu Button", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(310f, 76f));
        Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.15f, 0.67f, 0.96f, 1f);
        Outline buttonOutline = buttonRect.gameObject.AddComponent<Outline>();
        buttonOutline.effectColor = new Color(0.02f, 0.05f, 0.08f, 1f);
        buttonOutline.effectDistance = new Vector2(4f, -4f);

        mainMenuButton = buttonRect.gameObject.AddComponent<Button>();
        mainMenuButton.targetGraphic = buttonImage;
        ColorBlock colors = mainMenuButton.colors;
        colors.normalColor = new Color(0.15f, 0.67f, 0.96f, 1f);
        colors.highlightedColor = new Color(0.32f, 0.78f, 1f, 1f);
        colors.pressedColor = new Color(0.05f, 0.42f, 0.74f, 1f);
        colors.selectedColor = colors.highlightedColor;
        mainMenuButton.colors = colors;
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);

        TextMeshProUGUI buttonText = CreateText("Button Text", buttonRect, "MAIN MENU", 34f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        AddTextShadow(buttonText.gameObject, new Color(0f, 0f, 0f, 0.75f), new Vector2(2f, -2f));
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private static void AddTextShadow(GameObject target, Color color, Vector2 distance)
    {
        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
    }

    private void ReturnToMainMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMainMenu();
            return;
        }

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(MainMenuSceneName);
    }
}
