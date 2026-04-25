using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Sunlight Settings")]
    public float timeToDieInSun = 3f; // Time in seconds it takes to die in sunlight

    [Header("Audio Settings")]
    public AudioClip deathSFX; // Sound played when the player dies

    [Header("Visual Settings")]
    public GameObject deathVFXPrefab; // Prefab spawned when the player dies

    private bool isDying = false;
    private float sunExposureTimer = 0f;
    private float lastSunlightHitTime = -1f;

    private Burning burningScript;

    void Start()
    {
        // Automatically find the Burning script if it's attached to the player
        burningScript = GetComponentInChildren<Burning>();
    }

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
            // Turn on burning VFX
            if (burningScript != null) burningScript.SetBurningState(true);

            // Increase the timer
            sunExposureTimer += Time.deltaTime;

            if (sunExposureTimer >= timeToDieInSun)
            {
                Die();
            }
        }
        else
        {
            // Turn off burning VFX when in cover
            if (burningScript != null) burningScript.SetBurningState(false);

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
        
        // Turn off fire when dead
        if (burningScript != null) burningScript.SetBurningState(false);
        
        // Play the death sound
        if (deathSFX != null)
        {
            // We use PlayClipAtPoint so the sound finishes even though the player gets disabled!
            AudioSource.PlayClipAtPoint(deathSFX, transform.position);
        }

        // Spawn the death visual effect
        if (deathVFXPrefab != null)
        {
            // We instantiate it as a separate object so it doesn't vanish when the player is disabled!
            Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
        }

        Debug.Log("Player has died from sunlight.");
        // Add death logic here, such as showing death UI or destroying the object
        gameObject.SetActive(false);
    }
}
