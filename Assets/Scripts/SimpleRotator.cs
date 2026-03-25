using UnityEngine;

public class SimpleRotator : MonoBehaviour
{
    [Tooltip("传送门的旋转速度，数值越大转得越快，可以填负数反方向转")]
    public float rotationSpeed = 100f;

    void Update()
    {
        // 绕着自身的 Z 轴（圆盘的法线方向）不断旋转
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime, Space.Self);
    }
}