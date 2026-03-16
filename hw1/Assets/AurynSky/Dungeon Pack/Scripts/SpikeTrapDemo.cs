using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpikeTrapDemo : MonoBehaviour {

    public Animator spikeTrapAnim;
    public float activeTime = 2f;    // How long spikes stay up (dangerous)
    public float inactiveTime = 2f;  // How long spikes stay down (safe)

    [HideInInspector]
    public bool isActive = false;    // Other scripts can check this

    void Awake()
    {
        spikeTrapAnim = GetComponent<Animator>();
        StartCoroutine(OpenCloseTrap());
    }

    IEnumerator OpenCloseTrap()
    {
        // Spikes come up (DANGEROUS)
        spikeTrapAnim.SetTrigger("open");
        isActive = true;
        yield return new WaitForSeconds(activeTime);
        
        // Spikes retract (SAFE)
        spikeTrapAnim.SetTrigger("close");
        isActive = false;
        yield return new WaitForSeconds(inactiveTime);
        
        // Repeat
        StartCoroutine(OpenCloseTrap());
    }
}