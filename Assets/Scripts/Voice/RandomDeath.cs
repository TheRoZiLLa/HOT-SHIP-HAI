using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RandomDeath : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("Add all the possible voice lines/sounds here")]
    public AudioClip[] randomClips;

    private AudioSource audioSource;

    void Start()
    {
        // Get the AudioSource attached to this same object
        audioSource = GetComponent<AudioSource>();

        // Make sure we actually have clips in the list before trying to play one
        if (randomClips != null && randomClips.Length > 0)
        {
            // Pick a random number between 0 and the number of clips
            int randomIndex = Random.Range(0, randomClips.Length);
            
            // Assign the randomly chosen clip to the AudioSource and play it
            audioSource.clip = randomClips[randomIndex];
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("No audio clips have been added to the RandomDeath script on " + gameObject.name);
        }
    }
}
