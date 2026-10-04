using System.Collections.Generic;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [Tooltip("Si está activo, al spawnear reemplaza cualquier objeto de CUALQUIER spawner. Déjalo marcado en todos.")]
    [SerializeField] private bool onlyOneAtATime = true;
    [SerializeField] private GameObject prefab;
    [SerializeField] private Camera cam;

    [Header("Posición en pantalla (viewport 0-1)")]
    [SerializeField, Range(0f, 1f)] private float viewportX = 0.5f;
    [SerializeField, Range(0f, 1f)] private float viewportY = 0.4f;
    [SerializeField] private float distance = 0.6f;

    [Header("Rotación")]
    [SerializeField] private Vector3 spawnEulerAngles = new Vector3(-90f, 0f, 0f);
    [SerializeField] private bool relativeToCameraYaw = false;

    [Header("Tras lanzar")]
    [SerializeField] private float respawnDelay = 1f;
    [Header("Audio")]
    [SerializeField] private AudioClip spawnSfx;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private static readonly List<ItemSpawner> all = new List<ItemSpawner>();
    private static GameObject sharedCurrent;
    private GameObject ownCurrent;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
        CancelInvoke(nameof(Respawn));
    }

    public void Spawn()
    {
        CancelInvoke(nameof(Respawn));

        if (onlyOneAtATime)
        {
            foreach (var s in all)
                if (s != this) s.CancelInvoke(nameof(Respawn));

            if (sharedCurrent != null) Destroy(sharedCurrent);
            sharedCurrent = null;
        }
        else if (ownCurrent != null)
        {
            Destroy(ownCurrent);
        }
        ownCurrent = null;

        Vector3 pos = cam.ViewportToWorldPoint(new Vector3(viewportX, viewportY, distance));

        Quaternion rot = Quaternion.Euler(spawnEulerAngles);
        if (relativeToCameraYaw)
            rot = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f) * rot;

        GameObject obj = Instantiate(prefab, pos, rot);
        ownCurrent = obj;
        sharedCurrent = obj;

        if (obj.TryGetComponent(out ThrowableItem item))
        {
            item.Init(cam, new Vector2(viewportX, viewportY), distance);
            item.Thrown += OnItemThrown;
        }

        if (spawnSfx != null)
            AudioSource.PlayClipAtPoint(spawnSfx, pos, sfxVolume);
    }

    public void Despawn()
    {
        CancelInvoke(nameof(Respawn));

        if (ownCurrent != null)
        {
            if (sharedCurrent == ownCurrent) sharedCurrent = null;
            Destroy(ownCurrent);
        }
        ownCurrent = null;
    }

    public static void DespawnAll()
    {
        foreach (var s in all)
            if (s != null) s.Despawn();

        if (sharedCurrent != null) Destroy(sharedCurrent);
        sharedCurrent = null;
    }

    private void OnItemThrown(ThrowableItem item)
    {
        item.Thrown -= OnItemThrown;

        if (ownCurrent == item.gameObject) ownCurrent = null;
        if (sharedCurrent == item.gameObject) sharedCurrent = null;

        if (respawnDelay > 0f) Invoke(nameof(Respawn), respawnDelay);
    }

    private void Respawn()
    {
        if (!onlyOneAtATime || sharedCurrent == null) Spawn();
    }
}