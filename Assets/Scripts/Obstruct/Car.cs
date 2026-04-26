using UnityEngine;

public class Car : MonoBehaviour
{
    [Header("Collision Settings")]
    [Tooltip("Select the layer that your Player uses")]
    public LayerMask playerLayer;

    // This handles physical bumps/crashes
    private void OnCollisionEnter(Collision collision)
    {
        // Check if the object we hit is on the Player layer
        if (((1 << collision.gameObject.layer) & playerLayer.value) != 0)
        {
            KillPlayer(collision.gameObject);
        }
    }

    // This handles if the car is set to "Is Trigger"
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object we overlapped is on the Player layer
        if (((1 << other.gameObject.layer) & playerLayer.value) != 0)
        {
            KillPlayer(other.gameObject);
        }
    }

    private void KillPlayer(GameObject playerObj)
    {
        // Try to get the PlayerHealth script from the player
        PlayerHealth playerHealth = playerObj.GetComponent<PlayerHealth>();
        
        if (playerHealth != null)
        {
            Debug.Log("Car hit the player!");
            playerHealth.Die();
        }
        else
        {
            // Just in case the health script is on a parent/child object
            playerHealth = playerObj.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                Debug.Log("Car hit the player!");
                playerHealth.Die();
            }
        }
    }
}
