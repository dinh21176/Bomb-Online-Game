using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuButtonPolish : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private bool useTint;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = Color.white;
    [SerializeField] private Color pressedColor = new Color(0.78f, 0.92f, 1f, 1f);
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressedScale = 0.94f;
    [SerializeField] private float followSpeed = 22f;

    private Vector3 restScale;
    private Vector3 targetScale;
    private bool pointerInside;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        CaptureRestScale();

        if (useTint)
            ApplyColor(normalColor);
    }

    private void OnEnable()
    {
        CaptureRestScale();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * followSpeed);
    }

    private void OnValidate()
    {
        AutoAssignReferences();
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
        if (!Application.isPlaying) return;

        pointerInside = true;
        targetScale = restScale * hoverScale;

        if (useTint)
            ApplyColor(hoverColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!Application.isPlaying) return;

        pointerInside = false;
        targetScale = restScale;

        if (useTint)
            ApplyColor(normalColor);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Application.isPlaying) return;

        targetScale = restScale * pressedScale;

        if (useTint)
            ApplyColor(pressedColor);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!Application.isPlaying) return;

        targetScale = pointerInside ? restScale * hoverScale : restScale;

        if (useTint)
            ApplyColor(pointerInside ? hoverColor : normalColor);
    }

    private void AutoAssignReferences()
    {
        if (background == null)
            background = GetComponent<Image>();

        if (titleText == null)
            titleText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void CaptureRestScale()
    {
        restScale = transform.localScale;
        targetScale = restScale;
    }

    private void ApplyColor(Color color)
    {
        if (background != null)
            background.color = color;
    }
}
