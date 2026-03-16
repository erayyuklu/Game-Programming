using UnityEngine;

public class DoorController : MonoBehaviour
{
    private bool gameWon = false;
    private DoorScript.Door doorScript;

    void Start()
    {
        // Find the asset's Door script (might be on this object or a child)
        doorScript = GetComponentInChildren<DoorScript.Door>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Key") && !gameWon)
        {
            gameWon = true;
            Debug.Log("Door opened! Enter the room to win.");

            // Use the asset's built-in door open function
            if (doorScript != null)
            {
                doorScript.OpenDoor();
            }
        }
    }
}

