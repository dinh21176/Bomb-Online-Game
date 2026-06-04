using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ClickyButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Image _img;
    [SerializeField] private Sprite _default, _pressed;
    [SerializeField] private AudioClip _compressClip, _uncompressClip;
    [SerializeField] private AudioSource _source;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_img != null && _pressed != null)
            _img.sprite = _pressed;

        if (_source != null && _compressClip != null)
            _source.PlayOneShot(_compressClip);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_img != null && _default != null)
            _img.sprite = _default;

        if (_source != null && _uncompressClip != null)
            _source.PlayOneShot(_uncompressClip);
    }

    public void IWasClicked()
    {
        Debug.Log("Button was clicked!");
    }
}
