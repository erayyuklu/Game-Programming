using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 8f;
    public float rotationSpeed = 120f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Get input
        float moveInput = Input.GetAxis("Vertical");   // W/S or Up/Down arrows
        float turnInput = Input.GetAxis("Horizontal");  // A/D or Left/Right arrows

        // Rotate the player left/right
        float rotation = turnInput * rotationSpeed * Time.fixedDeltaTime;
        Quaternion turnRotation = Quaternion.Euler(0f, rotation, 0f);
        rb.MoveRotation(rb.rotation * turnRotation);

        // Move forward/backward using velocity instead of MovePosition
        // This prevents the physics engine from teleporting the player slightly into the wall
        Vector3 newVelocity = transform.forward * moveInput * moveSpeed;
        
        // Preserve existing Y velocity (for gravity to work properly later)
        newVelocity.y = rb.velocity.y;
        
        rb.velocity = newVelocity;
    }
}
