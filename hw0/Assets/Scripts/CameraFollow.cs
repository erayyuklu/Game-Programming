using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public Vector3 offset = new Vector3(0, 10, -10);

    void LateUpdate()
    {
        // only update the camera position after the player has moved
        transform.position = player.position + offset;
    }
}
