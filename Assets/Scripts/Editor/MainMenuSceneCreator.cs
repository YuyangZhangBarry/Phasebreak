using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates Assets/Scenes/MainMenu.unity and adds it as build index 0.
/// Menu: Phasebreaker > Create Main Menu Scene
/// </summary>
public static class MainMenuSceneCreator
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Phasebreaker/Create Main Menu Scene", false, 51)]
    public static void CreateMainMenuScene()
    {
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Camera cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null)
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.1f, 1f);

        GameObject menu = new GameObject("MainMenu");
        menu.AddComponent<MainMenuController>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[MainMenuSceneCreator] Saved {ScenePath}");

        AddSceneToBuildSettingsAsFirst(ScenePath);
    }

    private static void AddSceneToBuildSettingsAsFirst(string scenePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == scenePath)
            {
                var s = scenes[i];
                s.enabled = true;
                scenes.RemoveAt(i);
                scenes.Insert(0, s);
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log($"[MainMenuSceneCreator] Moved {scenePath} to build index 0.");
                return;
            }
        }

        var newScene = new EditorBuildSettingsScene(scenePath, true);
        scenes.Insert(0, newScene);
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[MainMenuSceneCreator] Added {scenePath} as build index 0.");
    }
}
