using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MultipleImagesTrackingManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> prefabsToSpawn = new List<GameObject>();
    [SerializeField] private BattleManager battleManager;

    [SerializeField] private StadiumPlacement stadium;

    [Header("Reset")]
    [SerializeField] private float resetEffectDuration = 0.8f;

    private ARTrackedImageManager _trackedImageManager;

    private Dictionary<string, GameObject> _arObjects;
    private HashSet<string> _registeredCards;

    private bool _resetting;

    private void Start()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();

        if (_trackedImageManager == null)
            return;

        _trackedImageManager.trackablesChanged.AddListener(OnImagesTrackedChanged);

        _arObjects = new Dictionary<string, GameObject>();
        _registeredCards = new HashSet<string>();

        SetupSceneElements();
    }

    private void OnDestroy()
    {
        if (_trackedImageManager != null)
        {
            _trackedImageManager.trackablesChanged.RemoveListener(OnImagesTrackedChanged);
        }
    }

    private void SetupSceneElements()
    {
        foreach (var prefab in prefabsToSpawn)
        {
            var arObject = Instantiate(prefab, Vector3.zero, Quaternion.identity);

            arObject.name = prefab.name;
            arObject.SetActive(false);

            _arObjects.Add(arObject.name, arObject);
        }
    }

    private void OnImagesTrackedChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            UpdateTrackedImage(trackedImage);
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            UpdateTrackedImage(trackedImage);
        }
    }

    private void UpdateTrackedImage(ARTrackedImage trackedImage)
    {
        if (_resetting || trackedImage == null)
            return;

        string imageName = trackedImage.referenceImage.name;

        if (!_arObjects.TryGetValue(imageName, out GameObject arObject))
            return;

        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            arObject.SetActive(true);

            bool lockedToStadium = stadium != null && stadium.IsLocked(imageName);

            if (!lockedToStadium)
            {
                arObject.transform.position = trackedImage.transform.position;
                arObject.transform.rotation = trackedImage.transform.rotation;
            }

            if (!_registeredCards.Contains(imageName))
            {
                _registeredCards.Add(imageName);

                if (stadium != null)
                {
                    stadium.RegisterCard(imageName, arObject);
                }
                else if (battleManager != null)
                {
                    battleManager.RegisterCard(imageName, arObject);
                }
            }
        }
    }

    public void ResetCards()
    {
        if (_resetting || _arObjects == null)
            return;

        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        _resetting = true;

        var effects = new List<PokemonCapture>();
        foreach (var obj in _arObjects.Values)
        {
            if (obj == null || !obj.activeInHierarchy) continue;

            foreach (var p in obj.GetComponentsInChildren<PokemonCapture>(false))
            {
                if (p == null || p.IsCaptured || p.IsBeingCaptured) continue;
                effects.Add(p);
            }
        }

        yield return new WaitUntil(() => effects.TrueForAll(p => p == null || !p.IsPlayingEffect));

        foreach (var p in effects)
            if (p != null) p.PlayDisappear(resetEffectDuration);

        yield return new WaitUntil(() => effects.TrueForAll(p => p == null || !p.IsPlayingEffect));

        _registeredCards.Clear();

        if (stadium != null)
            stadium.ResetStadium();

        foreach (var arObject in _arObjects.Values)
        {
            if (arObject != null)
                Destroy(arObject);
        }

        _arObjects.Clear();

        SetupSceneElements();

        _resetting = false;
    }
}