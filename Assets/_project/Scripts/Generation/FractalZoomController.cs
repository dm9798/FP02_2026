using UnityEngine;
using System.Collections;

public class FractalZoomController : MonoBehaviour
{
    public float zoomDuration = 1.5f;
    public Vector3 startScale = Vector3.one * 0.05f;
    public Vector3 targetScale = Vector3.one * 10f;

    bool isTransitioning = false;

    public void ZoomIntoChild(GameObject child, GameObject parentRoom)
    {
        if(isTransitioning)
            return; // Prevent multiple triggers during animation
        StartCoroutine(ScaleUpAndSwap(child, parentRoom));
    }

    IEnumerator ScaleUpAndSwap(GameObject child, GameObject parentRoom)
    {
        isTransitioning = true;

        child.SetActive(true);
        child.transform.localScale = startScale;

        float elapsed = 0;
        //not working - further investigation required
        while(elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / zoomDuration);
            child.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        child.transform.localScale = targetScale;
        parentRoom.SetActive(false);

        isTransitioning = false;
    }
}