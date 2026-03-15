using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TrapController : MonoBehaviour
{
    [Header("Trap Settings")]
    public float activeTime = 2f;    // How long the trap is active/deadly
    public float inactiveTime = 2f;  // How long the trap is inactive/safe

    [Header("Materials")]
    public Material lavaMaterial;     // Drag your lava material here (active/deadly)
    public Material waterMaterial;    // Drag your water material here (inactive/safe)
    
    private Renderer trapRenderer;
    private Collider trapCollider;

    void Start()
    {
        trapRenderer = GetComponent<Renderer>();
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
            if(trapRenderer != null) trapRenderer.material = waterMaterial;
            if(trapCollider != null) trapCollider.enabled = false; // Player can pass through safely
            
            yield return new WaitForSeconds(inactiveTime); // Wait

            // --- STATE: ACTIVE (DEADLY) ---
            if(trapRenderer != null) trapRenderer.material = lavaMaterial;
            if(trapCollider != null) trapCollider.enabled = true; // Player will trigger death
            
            yield return new WaitForSeconds(activeTime); // Wait
        }
    }

    // Called when another collider enters this trigger collider
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("PLAYER DIED! Hit by a Trap.");
            
            // For now, when the player dies, we just reload the current scene (restart the game)
            // We will improve this with a Game Over UI in the Bonus step.
            RestartGame();
        }
    }

    void RestartGame()
    {
        // Get the current active scene and load it again
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
