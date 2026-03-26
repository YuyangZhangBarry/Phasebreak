using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform camTransform;

    void LateUpdate()
    {
        if (camTransform == null)
        {
            if (Camera.main != null) camTransform = Camera.main.transform;
            else return;
        }

        // Keep UI rotation perfectly aligned with the camera.
        transform.rotation = camTransform.rotation;
    }
}