using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GuardPatrol : MonoBehaviour
{
    [Header("Patrol Settings")]
    public Transform pointA;  // Start point of patrol
    public Transform pointB;  // End point of patrol
    public float moveSpeed = 3f;

    [Header("Detection Settings")]
    public float detectionRange = 3f;  // How close the player must be to get caught

    private Transform player;

    void Start()
    {
        // Find the Player object by tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        // Start patrolling using Coroutine (Mandatory for HW1)
        StartCoroutine(PatrolRoutine());
    }

    // Coroutine: Guard moves back and forth between pointA and pointB
    IEnumerator PatrolRoutine()
    {
        while (true)
        {
            // Move towards point B
            yield return StartCoroutine(MoveToPoint(pointB.position));

            // Move towards point A
            yield return StartCoroutine(MoveToPoint(pointA.position));
        }
    }

    // Coroutine: Smoothly move the guard to a target position
    IEnumerator MoveToPoint(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.2f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                moveSpeed * Time.deltaTime
            );

            // While moving, always check if player is too close
            CheckPlayerDistance();

            yield return null; // Wait one frame
        }
    }

    // Check if the player is within detection range
    void CheckPlayerDistance()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= detectionRange)
        {
            Debug.Log("PLAYER DIED! Caught by a Guard.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    // Draw the detection range in the Scene view (helpful for debugging)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
