using UnityEngine;

public class MusicManager : MonoBehaviour
{
    private AudioSource audioSource;
    private bool isMuted = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        // Press M to toggle music on/off
        if (Input.GetKeyDown(KeyCode.M))
        {
            isMuted = !isMuted;
            audioSource.mute = isMuted;

            if (isMuted)
                Debug.Log("Music OFF");
            else
                Debug.Log("Music ON");
        }
    }
}
