using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to any GameObject in the GameOver scene.
/// Listens for any key/click to return to the main menu.
/// </summary>
public class GameOverScene : MonoBehaviour
{
    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            RestartGame();
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            RestartGame();
        }
    }

    public void RestartGame()
    {
        MenuNavigationHelper.LoadMainMenuAndCleanupRun();
    }
}
