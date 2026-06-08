using System.Collections;
using UnityEngine;

public class AirdropVisual : MonoBehaviour
{
    [SerializeField] private float dropDuration = 2.0f; 
    [SerializeField] private Vector3 startScale = new Vector3(3f, 3f, 3f); 
    [SerializeField] private Vector3 endScale = new Vector3(1f, 1f, 1f);  

    private void Start()
    {
        StartCoroutine(DropAnimation());
    }

    private IEnumerator DropAnimation()
    {
        float timer = 0f;
        transform.localScale = startScale;

        // Tùy chọn: Đổi màu nhấp nháy hoặc thêm bóng mờ ở đây

        while (timer < dropDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / dropDuration;

            // Hiệu ứng thu nhỏ dần (giả lập việc rơi từ xa xuống gần)
            transform.localScale = Vector3.Lerp(startScale, endScale, progress);

            yield return null;
        }

        // Rơi xong thì tự hủy cái vỏ ảo ảnh này đi, nhường chỗ cho Item thật xuất hiện
        Destroy(gameObject);
    }
}