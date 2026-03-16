using UnityEngine;
using UnityEngine.SceneManagement;

public class KillZone : MonoBehaviour
{
    private SpikeTrapDemo spikeTrap;

    void Start()
    {
        // Find the SpikeTrapDemo on the parent
        spikeTrap = GetComponentInParent<SpikeTrapDemo>();

        // Make sure collider is a trigger and ALWAYS enabled
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    // No Update() - collider stays enabled all the time

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && spikeTrap != null && spikeTrap.isActive)
        {
            Debug.Log("PLAYER DIED! Spike trap isActive=" + spikeTrap.isActive);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
