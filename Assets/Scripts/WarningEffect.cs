using UnityEngine;

public class WarningEffect : MonoBehaviour
{
    [SerializeField] private float blinkSpeed = 10f;
    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (sr == null) return;

        // Oscillate transparency between 0.2 and 0.6
        float alpha = Mathf.PingPong(Time.time * blinkSpeed, 0.4f) + 0.2f;
        Color c = sr.color;
        c.a = alpha;
        sr.color = c;
    }
}