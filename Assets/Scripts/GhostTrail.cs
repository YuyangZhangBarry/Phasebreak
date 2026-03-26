using UnityEngine;
using System.Collections;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GhostTrail : MonoBehaviour
{
    [Header("Ghost Settings")]
    public float ghostLifetime = 0.5f; // ghost lifetime
    public float spawnRate = 0.05f;    // spawn frequency (smaller = denser ghosts)
    public Material ghostMaterial;     // semi-transparent material for ghosts

    private MeshFilter meshFilter;
    private bool isTrailing = false;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    // Public interface for PlayerController.
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
        // 1) Create an empty GameObject as the ghost clone.
        GameObject ghostObj = new GameObject("Ghost_Clone");
        ghostObj.transform.position = transform.position;
        ghostObj.transform.rotation = transform.rotation;
        ghostObj.transform.localScale = transform.localScale;

        // 2) Copy the current mesh shape from the player.
        MeshFilter gf = ghostObj.AddComponent<MeshFilter>();
        MeshRenderer gr = ghostObj.AddComponent<MeshRenderer>();
        gf.mesh = meshFilter.mesh;
        
        // 3) Assign the semi-transparent material.
        if (ghostMaterial != null)
        {
            gr.material = ghostMaterial;
        }

        // 4) Start fading coroutine and destroy the ghost at the end.
        StartCoroutine(FadeGhost(ghostObj, gr.material));
    }

    private IEnumerator FadeGhost(GameObject ghost, Material mat)
    {
        float elapsedTime = 0f;
        Color startColor = mat.color;

        while (elapsedTime < ghostLifetime)
        {
            elapsedTime += Time.deltaTime;
            // Compute current alpha (from 1 to 0).
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / ghostLifetime);
            mat.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null; // wait for next frame
        }

        Destroy(ghost);
    }
}