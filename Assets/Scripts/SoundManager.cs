using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public AudioSource audioSource;

    void Start()
    {
        // Play sound at the start of the game
        audioSource.Play();
    }

    void Update()
    {
        // Toggle sound on/off when the M key is pressed
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (audioSource.isPlaying)
                audioSource.Pause();
            else
                audioSource.UnPause();
        }
    }
}
