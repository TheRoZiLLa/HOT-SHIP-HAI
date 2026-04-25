using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Sunlight Settings")]
    public float timeToDieInSun = 3f; // Time in seconds it takes to die in sunlight

    private bool isDying = false;
    private float sunExposureTimer = 0f;
    private float lastSunlightHitTime = -1f;

    // This is called every frame by the Sunlight script when the raycast hits the player
    public void HitBySunlight()
    {
        if (isDying) return;
        
        // Update the timestamp of when we were last hit
        lastSunlightHitTime = Time.time;
    }

    void Update()
    {
        if (isDying) return;

        // If we were hit by sunlight within the last 0.1 seconds, we are "in the sun"
        if (Time.time - lastSunlightHitTime <= 0.1f)
        {
            // Increase the timer
            sunExposureTimer += Time.deltaTime;

            if (sunExposureTimer >= timeToDieInSun)
            {
                Die();
            }
        }
        else
        {
            // We are NOT in the sun (we took cover). Reset the timer!
            if (sunExposureTimer > 0f)
            {
                sunExposureTimer = 0f;
                Debug.Log("Player took cover! Sun timer reset.");
            }
        }
    }

    public void Die()
    {
        isDying = true;
        Debug.Log("Player has died from sunlight.");
        // Add death logic here, such as showing death UI or destroying the object
        gameObject.SetActive(false);
    }
}
