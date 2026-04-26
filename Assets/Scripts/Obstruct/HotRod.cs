using UnityEngine;

public class HotRod : MonoBehaviour
{
    [Header("Metal Floor Settings")]
    [Tooltip("The layer(s) considered as 'Metal'")]
    public LayerMask metalLayer;
    public float timeToDieOnMetal = 3f;
    [Tooltip("Distance to check for the floor below the player")]
    public float raycastDistance = 1.5f;

    private float metalExposureTimer = 0f;
    private PlayerHealth playerHealth;

    void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        if (playerHealth == null) return;

        // Cast a ray downwards to check the floor
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, raycastDistance))
        {
            // Check if the floor we are standing on is in the Metal layer mask
            if (((1 << hit.collider.gameObject.layer) & metalLayer.value) != 0)
            {
                metalExposureTimer += Time.deltaTime;

                if (metalExposureTimer >= timeToDieOnMetal)
                {
                    playerHealth.Die();
                }
            }
            else
            {
                // We are on a floor, but it's not metal. Reset the timer.
                if (metalExposureTimer > 0f)
                {
                    metalExposureTimer = 0f;
                    Debug.Log("Player moved off metal floor! Metal timer reset.");
                }
            }
        }
        else
        {
            // Not on any floor (jumping/falling). Reset the timer.
            if (metalExposureTimer > 0f)
            {
                metalExposureTimer = 0f;
            }
        }
    }
}
