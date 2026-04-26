using UnityEngine;

public class UnlockMouse : MonoBehaviour
{
    void Start()
    {
        // Unlock the mouse so it can move freely across the screen
        Cursor.lockState = CursorLockMode.None;
        
        // Make sure the cursor is actually visible
        Cursor.visible = true;
    }
}
