using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    private bool isDying = false;

    // Call this when the player is hit by the sunlight beam
    public void HitBySunlight()
    {
        if (!isDying)
        {
            StartCoroutine(DeathRoutine());
        }
    }

    private IEnumerator DeathRoutine()
    {
        isDying = true;
        Debug.Log("Hit by sunlight! Player will die in 3 seconds.");
        
        // Wait for 3 seconds
        yield return new WaitForSeconds(3f);
        
        Die();
    }

    private void Die()
    {
        Debug.Log("Player has died.");
        // Add death logic here, such as showing death UI or destroying the object
        gameObject.SetActive(false);
    }
}
