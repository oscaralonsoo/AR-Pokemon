using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [SerializeField] private bool onlyOneAtATime = true;

    [SerializeField] private GameObject prefab;
    [SerializeField] private Camera cam;

    [Header("Posición en pantalla (viewport 0-1)")]
    [SerializeField, Range(0f, 1f)] private float viewportX = 0.5f;
    [SerializeField, Range(0f, 1f)] private float viewportY = 0.35f;
    [SerializeField] private float distance = 0.6f;

    [Header("Rotación")]
    [SerializeField] private Vector3 spawnEulerAngles = new Vector3(-90f, 0f, 0f);
    [SerializeField] private bool relativeToCameraYaw = false;

    [Header("Audio")]
    [SerializeField] private AudioClip spawnSfx;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private static GameObject sharedCurrent; 
    private GameObject ownCurrent;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    public void Spawn()
    {
        if (onlyOneAtATime)
        {
            if (sharedCurrent != null) Destroy(sharedCurrent);
        }
        else if (ownCurrent != null)
        {
            Destroy(ownCurrent);
        }

        Vector3 pos = cam.ViewportToWorldPoint(new Vector3(viewportX, viewportY, distance));

        Quaternion rot = Quaternion.Euler(spawnEulerAngles);
        if (relativeToCameraYaw)
            rot = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f) * rot;

        GameObject obj = Instantiate(prefab, pos, rot);
        ownCurrent = obj;
        sharedCurrent = obj;

        if (spawnSfx != null)
            AudioSource.PlayClipAtPoint(spawnSfx, pos, sfxVolume);
    }
}