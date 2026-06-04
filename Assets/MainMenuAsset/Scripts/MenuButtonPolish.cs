using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuButtonPolish : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.94f);
    [SerializeField] private Color pressedColor = new Color(0.78f, 0.92f, 1f, 1f);
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float pressedScale = 0.96f;
    [SerializeField] private float followSpeed = 18f;

    private Vector3 targetScale;
    private bool pointerInside;

    private void Awake()
    {
        if (background == null)
            background = GetComponent<Image>();

        targetScale = Vector3.one;
        ApplyColor(normalColor);
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * followSpeed);
    }

    public void SetCopy(string title, string subtitle)
    {
        if (titleText != null)
            titleText.text = title;

        if (subtitleText != null)
            subtitleText.text = subtitle;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        targetScale = Vector3.one * hoverScale;
        ApplyColor(hoverColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        targetScale = Vector3.one;
        ApplyColor(normalColor);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = Vector3.one * pressedScale;
        ApplyColor(pressedColor);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = pointerInside ? Vector3.one * hoverScale : Vector3.one;
        ApplyColor(pointerInside ? hoverColor : normalColor);
    }

    private void ApplyColor(Color color)
    {
        if (background != null)
            background.color = color;
    }
}
