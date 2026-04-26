using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Header("Scene Transition Settings")]
    [Tooltip("The exact name of the scene you want to load")]
    public string sceneToLoad;

    /// <summary>
    /// Call this method from the UI Button's OnClick event
    /// </summary>
    public void LoadTargetScene()
    {
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.Log("Loading scene: " + sceneToLoad);
            
            // VERY IMPORTANT: If the game was paused (like during a death screen), 
            // we must unpause it before loading the new scene!
            Time.timeScale = 1f; 
            
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogWarning("Scene to load is not set on " + gameObject.name);
        }
    }
}
