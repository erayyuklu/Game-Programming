using UnityEngine;

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

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && spikeTrap != null && spikeTrap.isActive)
        {
            GameManager.LoseGame();
        }
    }
}
