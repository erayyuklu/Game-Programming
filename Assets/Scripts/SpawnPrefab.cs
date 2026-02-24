using UnityEngine;

public class SpawnPrefab : MonoBehaviour
{
    
    public GameObject prefabToSpawn;

    // Spawn position offset from the current object
    public Vector3 spawnOffset = new Vector3(0, 5, 0);

    void Update()
    {
        // Spawn the prefab when the space key is pressed
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Vector3 spawnPosition = transform.position + spawnOffset;
            Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        }
    }
}
