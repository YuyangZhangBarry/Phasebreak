using UnityEngine;

public class SimpleRotator : MonoBehaviour
{
    [Tooltip("Portal rotation speed. Higher values rotate faster; negative values rotate in the opposite direction.")]
    public float rotationSpeed = 100f;

    void Update()
    {
        // Rotate continuously around the local Z axis (disk normal direction).
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime, Space.Self);
    }
}