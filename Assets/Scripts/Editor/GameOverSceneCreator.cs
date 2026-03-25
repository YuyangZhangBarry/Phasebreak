using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Editor tool: creates a GameOver scene with UI and adds it to Build Settings.
/// Menu: Phasebreaker > Create GameOver Scene
/// </summary>
public static class GameOverSceneCreator
{
    private const string ScenePath = "Assets/Scenes/GameOver.unity";

    [MenuItem("Phasebreaker/Create GameOver Scene", false, 50)]
    public static void CreateGameOverScene()
    {
        // Save any open scene changes first
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        // Create a brand new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // --- Camera background: dark ---
        Camera cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null)
            cam.backgroundColor = new Color(0.05f, 0.0f, 0.0f, 1f);

        // --- Canvas ---
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // --- EventSystem ---
        GameObject esGo = new GameObject("EventSystem");
        esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // --- Dark fullscreen background panel ---
        GameObject bgPanel = CreateUIElement("Background", canvasGo.transform);
        RectTransform bgRect = bgPanel.GetComponent<RectTransform>();
        StretchFull(bgRect);
        Image bgImg = bgPanel.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0f, 0f, 1f);

        // --- "GAME OVER" title ---
        GameObject titleGo = CreateUIElement("GameOverTitle", bgPanel.transform);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.65f);
        titleRect.anchorMax = new Vector2(0.5f, 0.65f);
        titleRect.sizeDelta = new Vector2(900f, 160f);
        titleRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "GAME OVER";
        titleText.fontSize = 100;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.red;
        titleText.fontStyle = FontStyles.Bold;

        // --- "Press any key to restart" subtitle ---
        GameObject subGo = CreateUIElement("SubtitleText", bgPanel.transform);
        RectTransform subRect = subGo.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.4f);
        subRect.anchorMax = new Vector2(0.5f, 0.4f);
        subRect.sizeDelta = new Vector2(700f, 80f);
        subRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI subText = subGo.AddComponent<TextMeshProUGUI>();
        subText.text = "Press any key to restart";
        subText.fontSize = 36;
        subText.alignment = TextAlignmentOptions.Center;
        subText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        // --- GameOverScene script on a manager object ---
        GameObject manager = new GameObject("GameOverManager");
        manager.AddComponent<GameOverScene>();

        // --- Save the scene ---
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[GameOverSceneCreator] Scene saved to {ScenePath}");

        // --- Add to Build Settings if not already there ---
        AddSceneToBuildSettings(ScenePath);

        // Also make sure all Level scenes are in Build Settings
        EnsureLevelScenesInBuildSettings();

        Debug.Log("[GameOverSceneCreator] Done! GameOver scene created and added to Build Settings.");
    }

    private static GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        foreach (var s in scenes)
        {
            if (s.path == scenePath)
            {
                s.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"[GameOverSceneCreator] {scenePath} already in Build Settings (ensured enabled).");
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[GameOverSceneCreator] Added {scenePath} to Build Settings.");
    }

    private static void EnsureLevelScenesInBuildSettings()
    {
        string[] requiredScenes = new string[]
        {
            "Assets/Scenes/Level_01.unity",
            "Assets/Scenes/Level_02.unity",
            "Assets/Scenes/Level_03.unity",
            "Assets/Scenes/Level_04.unity",
        };

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool changed = false;

        foreach (string path in requiredScenes)
        {
            bool found = false;
            foreach (var s in scenes)
            {
                if (s.path == path)
                {
                    found = true;
                    break;
                }
            }

            if (!found && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
                changed = true;
                Debug.Log($"[GameOverSceneCreator] Added missing scene to Build Settings: {path}");
            }
        }

        if (changed)
            EditorBuildSettings.scenes = scenes.ToArray();
    }
}
