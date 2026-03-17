using UnityEngine;

public class WaterAnimation : MonoBehaviour
{
    public float scrollSpeedX = 0.05f;  // Horizontal wave speed
    public float scrollSpeedY = 0.03f;  // Vertical wave speed

    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        // Animate the texture offset to simulate waves
        float offsetX = Time.time * scrollSpeedX;
        float offsetY = Time.time * scrollSpeedY;
        rend.material.mainTextureOffset = new Vector2(offsetX, offsetY);
    }
}
