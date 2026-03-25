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

        // 让 UI 的旋转永远和摄像机的旋转保持完全一致
        transform.rotation = camTransform.rotation;
    }
}