using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TrapController : MonoBehaviour
{
    [Header("Trap Settings")]
    public float activeTime = 2f;    // How long the trap is active/deadly
    public float inactiveTime = 2f;  // How long the trap is inactive/safe
    
    private Collider trapCollider;

    void Start()
    {
        trapCollider = GetComponent<Collider>();
        
        // Ensure the collider is a trigger
        if(trapCollider != null)
        {
            trapCollider.isTrigger = true;
        }

        // Start the periodic toggle using Coroutine (Mandatory for HW1)
        StartCoroutine(ToggleTrapRoutine());
    }

    // This Coroutine handles the periodic activation/deactivation of the trap
    IEnumerator ToggleTrapRoutine()
    {
        while (true) // Loop forever
        {
            // --- STATE: INACTIVE (SAFE) ---
            if(trapCollider != null) trapCollider.enabled = false;
            
            yield return new WaitForSeconds(inactiveTime);

            // --- STATE: ACTIVE (DEADLY) ---
            if(trapCollider != null) trapCollider.enabled = true;
            
            yield return new WaitForSeconds(activeTime);
        }
    }

    // Called when another collider enters this trigger collider
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("PLAYER DIED! Hit by a Trap.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
