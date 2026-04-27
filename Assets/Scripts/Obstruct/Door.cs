using UnityEngine;
using UnityEngine.SceneManagement;

public class Door : MonoBehaviour
{
    [Tooltip("The exact name of the scene to load.")]
    public string sceneToLoad;

    // 3D Collision
    private void OnCollisionEnter(Collision collision)
    {
        CheckAndTransition(collision.gameObject);
    }

    // 3D Trigger
    private void OnTriggerEnter(Collider other)
    {
        CheckAndTransition(other.gameObject);
    }

    // 2D Collision
    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckAndTransition(collision.gameObject);
    }

    // 2D Trigger
    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckAndTransition(other.gameObject);
    }

    // Character Controller hit
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        CheckAndTransition(hit.gameObject);
    }

    private void CheckAndTransition(GameObject hitObject)
    {
        // Check if the object has the "Player" tag OR the "Player" layer
        bool isPlayerTag = hitObject.CompareTag("Player");
        bool isPlayerLayer = hitObject.layer == LayerMask.NameToLayer("Player");

        if (isPlayerTag || isPlayerLayer)
        {
            Debug.Log("Door: Player hit detected! Attempting to load scene: " + sceneToLoad);

            if (!string.IsNullOrEmpty(sceneToLoad))
            {
                try
                {
                    SceneManager.LoadScene(sceneToLoad);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Door: Failed to load scene. Is it added to the Build Settings? Error: " + e.Message);
                }
            }
            else
            {
                Debug.LogWarning("Door: Scene to load is empty! Please type the exact scene name in the Inspector.");
            }
        }
    }
}
