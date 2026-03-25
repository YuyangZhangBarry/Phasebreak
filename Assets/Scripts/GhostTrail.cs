using UnityEngine;
using System.Collections;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GhostTrail : MonoBehaviour
{
    [Header("Ghost Settings")]
    public float ghostLifetime = 0.5f; // 残影存活时间
    public float spawnRate = 0.05f;    // 生成频率（越小残影越密集）
    public Material ghostMaterial;     // 残影专用的半透明材质

    private MeshFilter meshFilter;
    private bool isTrailing = false;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    // 提供给 PlayerController 调用的接口
    public void StartTrail()
    {
        isTrailing = true;
        StartCoroutine(SpawnTrail());
    }

    public void StopTrail()
    {
        isTrailing = false;
    }

    private IEnumerator SpawnTrail()
    {
        while (isTrailing)
        {
            GenerateGhost();
            yield return new WaitForSeconds(spawnRate);
        }
    }

    private void GenerateGhost()
    {
        // 1. 创建一个空物体作为残影
        GameObject ghostObj = new GameObject("Ghost_Clone");
        ghostObj.transform.position = transform.position;
        ghostObj.transform.rotation = transform.rotation;
        ghostObj.transform.localScale = transform.localScale;

        // 2. 赋予玩家当前的网格形状
        MeshFilter gf = ghostObj.AddComponent<MeshFilter>();
        MeshRenderer gr = ghostObj.AddComponent<MeshRenderer>();
        gf.mesh = meshFilter.mesh;
        
        // 3. 赋予半透明材质
        if (ghostMaterial != null)
        {
            gr.material = ghostMaterial;
        }

        // 4. 开启褪色协程，并在结束时销毁它
        StartCoroutine(FadeGhost(ghostObj, gr.material));
    }

    private IEnumerator FadeGhost(GameObject ghost, Material mat)
    {
        float elapsedTime = 0f;
        Color startColor = mat.color;

        while (elapsedTime < ghostLifetime)
        {
            elapsedTime += Time.deltaTime;
            // 计算当前的透明度 (从 1 到 0)
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / ghostLifetime);
            mat.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null; // 等待下一帧
        }

        Destroy(ghost);
    }
}