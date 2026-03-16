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
            GameManager.WinGame();
        }
    }
}
