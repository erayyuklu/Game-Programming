using UnityEngine;

public class DoorController : MonoBehaviour
{
    private bool gameWon = false;

    // This function is called when another object's collider hits this object's collider
    private void OnCollisionEnter(Collision collision)
    {
        // Check if the object we collided with has the tag "Key"
        if (collision.gameObject.CompareTag("Key") && !gameWon)
        {
            gameWon = true;
            Debug.Log("YOU WIN! The key has reached the door.");
            
            // Optional: Destroy or disable the door to show it's "opened"
            gameObject.SetActive(false);
            
            // Optional: Destroy the key as well (uncomment if you want)
            // Destroy(collision.gameObject);
        }
    }
}
