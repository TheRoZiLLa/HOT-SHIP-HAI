using UnityEngine;
using UnityEngine.SceneManagement;

public class Transition : MonoBehaviour
{
    [Header("Scene Transition Settings")]
    [Tooltip("The exact name of the scene you want to load")]
    public string sceneToLoad;

    /// <summary>
    /// Call this method from an Animation Event at the end of your animation clip!
    /// </summary>
    public void OnAnimationEnd()
    {
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.Log("Animation ended. Loading scene: " + sceneToLoad);
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogWarning("Scene to load is not set on " + gameObject.name);
        }
    }
}
