using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public Rigidbody rb;
    public float moveForce = 10f;

    void FixedUpdate()
    {
        float horizontal = Input.GetAxis("Horizontal"); // A-D or Left-Right arrow
        float vertical = Input.GetAxis("Vertical");     // W-S or Up-Down arrow

        Vector3 movement = new Vector3(horizontal, 0, vertical);
        rb.AddForce(movement * moveForce);
    }
}
