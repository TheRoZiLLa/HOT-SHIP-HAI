using UnityEngine;

public class Sunlight : MonoBehaviour
{
    [Header("Raycast Settings")]
    public float rayDistance = 100f;
    // We removed hitMask so the ray automatically hits the very first physical object in the scene (walls, floor, player, etc.)

    private Transform playerTransform;

    void Start()
    {
        // Find the player at the start using the "Player" tag
        FindPlayer();
    }

    void Update()
    {
        // If player is null (e.g. destroyed), try to find it again
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return; // Still not found, do nothing
        }

        // Calculate direction from the sunlight source to the player
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;

        // Make the sunlight beam object rotate to actually look at the player
        transform.rotation = Quaternion.LookRotation(directionToPlayer);

        RaycastHit hit;

        // Cast a ray towards the player to check for line of sight
        // By not using a LayerMask, it will literally hit the very first thing it sees
        if (Physics.Raycast(transform.position, directionToPlayer, out hit, rayDistance))
        {
            // Check if it's the player either by Tag or by the fact it has PlayerHealth
            PlayerHealth playerHealth = hit.collider.GetComponentInParent<PlayerHealth>();

            // If the first object hit is the player, there is no obstacle in front of them
            if (hit.collider.CompareTag("Player") || playerHealth != null)
            {
                if (playerHealth != null)
                {
                    // Notify the player they were hit by the sunlight beam
                    playerHealth.HitBySunlight();
                }
                else
                {
                    Debug.LogWarning("The sunlight hit the Player, but could not find the PlayerHealth script! Make sure PlayerHealth is attached to the player.");
                }
            }
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }
}
