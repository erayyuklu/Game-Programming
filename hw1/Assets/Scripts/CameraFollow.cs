using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;          // The player to follow
    public Vector3 offset = new Vector3(0f, 10f, -6f);  // Camera offset from player
    public float smoothSpeed = 5f;    // How smoothly camera follows

    void LateUpdate()
    {
        if (target == null) return;

        // Desired position: player position + offset (rotated with the player)
        Vector3 desiredPosition = target.position + target.rotation * offset;

        // Smoothly move camera to desired position
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;

        // Always look at the player
        transform.LookAt(target);
    }
}
