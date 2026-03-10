using UnityEngine;

public class AddForce : MonoBehaviour
{

    public Rigidbody rb;

    // Force amount
    public float forceAmount = 5f;

    // Force direction
    public Vector3 forceDirection = Vector3.forward;

    void FixedUpdate()
    {
        // Apply force to the Rigidbody in each physics update
        rb.AddForce(forceDirection * forceAmount);
    }
}
