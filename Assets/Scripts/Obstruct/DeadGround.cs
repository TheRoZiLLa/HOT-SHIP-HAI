using UnityEngine;

public class DeadGround : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        CheckAndKill(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckAndKill(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckAndKill(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckAndKill(other.gameObject);
    }

    private void CheckAndKill(GameObject hitObject)
    {
        // Check if the object has the "Player" tag OR the "Player" layer
        bool isPlayerTag = hitObject.CompareTag("Player");
        bool isPlayerLayer = hitObject.layer == LayerMask.NameToLayer("Player");

        if (isPlayerTag || isPlayerLayer)
        {
            PlayerHealth health = hitObject.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Die();
            }
            else
            {
                // Fallback: If PlayerHealth is on a parent/child object
                health = hitObject.GetComponentInParent<PlayerHealth>();
                if (health == null) health = hitObject.GetComponentInChildren<PlayerHealth>();
                
                if (health != null)
                {
                    health.Die();
                }
                else
                {
                    Debug.LogWarning("DeadGround: Player detected, but no PlayerHealth script found on " + hitObject.name);
                }
            }
        }
    }
}
