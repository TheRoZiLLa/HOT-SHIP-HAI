using UnityEngine;

public class HotRod : MonoBehaviour
{
    [Header("Metal Floor Settings")]
    [Tooltip("The layer(s) considered as 'Metal'")]
    public LayerMask metalLayer;
    public float timeToDieOnMetal = 3f;
    
    [Tooltip("How far down to look for the floor")]
    public float raycastDistance = 1.5f;
    
    [Tooltip("Starting height offset for the ray (to avoid hitting the player's own feet)")]
    public float rayOriginOffset = 0.5f;

    private float metalExposureTimer = 0f;
    private PlayerHealth playerHealth;

    void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.LogError("HotRod: No PlayerHealth script found on this object!");
        }
    }

    void Update()
    {
        if (playerHealth == null) return;

        // Start the ray slightly above the player's position to avoid starting inside the floor
        Vector3 rayOrigin = transform.position + Vector3.up * rayOriginOffset;
        
        // Draw the ray in the Scene view so you can see it while playing!
        Debug.DrawRay(rayOrigin, Vector3.down * raycastDistance, Color.red);

        // Cast the ray downwards
        // We use the metalLayer here so it ONLY hits objects on that layer
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, metalLayer))
        {
            metalExposureTimer += Time.deltaTime;
            Debug.Log("On Metal Floor! Timer: " + metalExposureTimer.ToString("F2"));

            if (metalExposureTimer >= timeToDieOnMetal)
            {
                playerHealth.Die();
                metalExposureTimer = 0f;
            }
        }
        else
        {
            // Not on a metal floor. Reset the timer.
            if (metalExposureTimer > 0f)
            {
                metalExposureTimer = 0f;
                Debug.Log("Player off metal. Timer reset.");
            }
        }
    }
}
