using UnityEngine;

// Keeps the separate animated weapon aligned with the camera-facing character art.
[ExecuteAlways]
public class SurvivalBatMount : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera != null) transform.rotation = camera.transform.rotation;
    }
}
