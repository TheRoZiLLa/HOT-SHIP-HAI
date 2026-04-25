using UnityEngine;
using TMPro; // TextMeshPro namespace for modern Unity UI text

public class Timer : MonoBehaviour
{
    [Header("Timer Settings")]
    public float totalTimeInSeconds = 600f; // 600 seconds = 10 minutes
    
    private float timeRemaining;
    private bool timerRunning = true;

    [Header("UI Slots")]
    public TMP_Text timerText; // Drag your TextMeshPro UI Text component here in the Inspector

    void Start()
    {
        // Initialize the timer
        timeRemaining = totalTimeInSeconds;
    }

    void Update()
    {
        if (timerRunning)
        {
            if (timeRemaining > 0)
            {
                // Decrease timer by the time passed since last frame
                timeRemaining -= Time.deltaTime;
                UpdateTimerUI(timeRemaining);
            }
            else
            {
                // Timer hit zero!
                timeRemaining = 0;
                timerRunning = false;
                UpdateTimerUI(timeRemaining);
                TimeUpDeath();
            }
        }
    }

    void UpdateTimerUI(float timeToDisplay)
    {
        if (timerText != null)
        {
            // Calculate minutes and seconds
            float minutes = Mathf.FloorToInt(timeToDisplay / 60); 
            float seconds = Mathf.FloorToInt(timeToDisplay % 60);

            // Update the text string to format MM:SS
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    void TimeUpDeath()
    {
        Debug.Log("Time is up! The player dies.");
        
        // Find the player using the "Player" tag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Grab the PlayerHealth script and trigger the death method
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Die();
            }
            else
            {
                // Fallback: If they don't have the PlayerHealth script, just disable the player object directly
                player.SetActive(false);
            }
        }
    }
}
