using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;           
using UnityEngine.XR.Interaction.Toolkit.Interactables; 

[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class ThrowablePokeball : MonoBehaviour
{
    [SerializeField] private float destroyAfterThrow = 8f;

    private Rigidbody rb;
    private XRGrabInteractable grab;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();

        rb.useGravity = false; 

        grab.selectExited.AddListener(OnReleased);
    }

    void OnDestroy()
    {
        if (grab != null) grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        rb.useGravity = true;
        Destroy(gameObject, destroyAfterThrow);
    }
}