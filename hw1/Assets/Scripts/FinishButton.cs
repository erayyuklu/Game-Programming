using UnityEngine;

public class FinishButton : MonoBehaviour
{
    void Start()
    {
        // Make sure collider is a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("YOU WIN! Player reached the finish zone.");
            // We will add Game Over UI later in the bonus step
            // For now, just log the win message
        }
    }
}
