using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MultipleImagesTrackingManager : MonoBehaviour
{
    [SerializeField] List<GameObject> prefabsToSpawn = new List<GameObject>();

    private ARTrackedImageManager _trackedImageManager;

    private Dictionary<string, GameObject> _arObjects;

    private void Start()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
        if (_trackedImageManager == null) return;

        _trackedImageManager.trackablesChanged.AddListener(OnImagesTrackedChanged);

        _arObjects = new Dictionary<string, GameObject>();

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

    private void OnImagesTrackedChanged(
        ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            UpdateTrackedImage(trackedImage);
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            UpdateTrackedImage(trackedImage);
        }

/*        foreach (var trackedImage in eventArgs.removed)
        {
        }*/
    }

    private void UpdateTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage == null) return;

        string imageName = trackedImage.referenceImage.name;

        if (!_arObjects.TryGetValue(imageName, out GameObject arObject))
            return;

        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            arObject.SetActive(true);

            arObject.transform.position = trackedImage.transform.position;
            arObject.transform.rotation = trackedImage.transform.rotation;
        }
    }
}