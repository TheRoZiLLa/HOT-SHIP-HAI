using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RareDeath : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("Add all the possible voice lines/sounds here")]
    public AudioClip[] randomClips;

    [Header("Rarity Settings")]
    [Tooltip("Percentage chance for the sound to play (0 to 100)")]
    [Range(0f, 100f)]
    public float playChance = 10f; // Default 10% chance

    private AudioSource audioSource;

    void Awake()
    {
        // Roll the dice! (Pick a random number between 0 and 100)
        float randomRoll = Random.Range(0f, 100f);

        // If the roll is less than or equal to our chance, we play the rare sound!
        if (randomRoll <= playChance)
        {
            // We succeeded! Find any normal RandomDeath scripts and turn them off 
            // so they don't play at the same time.
            RandomDeath[] normalDeaths = FindObjectsOfType<RandomDeath>();
            foreach (RandomDeath normal in normalDeaths)
            {
                normal.enabled = false;
            }
        }
        else
        {
            // We failed the rarity check. We disable OURSELVES so our Start() doesn't run,
            // which allows the normal RandomDeath script to play instead!
            this.enabled = false;
        }
    }

    void Start()
    {
        // Start is only called if this script wasn't disabled in Awake
        audioSource = GetComponent<AudioSource>();

        if (randomClips != null && randomClips.Length > 0)
        {
            int randomIndex = Random.Range(0, randomClips.Length);
            audioSource.clip = randomClips[randomIndex];
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("No audio clips have been added to the RareDeath script on " + gameObject.name);
        }
    }
}
