using UnityEngine;

public class GroupAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private bool startVisible;
    [SerializeField] private string visibleState = "Left_In"; 

    private static readonly int InHash = Animator.StringToHash("in");
    private static readonly int OutHash = Animator.StringToHash("out");

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    void Start()
    {
        if (startVisible)
            animator.Play(visibleState, 0, 1f);
    }

    public void Show()
    {
        animator.ResetTrigger(OutHash);
        animator.SetTrigger(InHash);
    }

    public void Hide()
    {
        animator.ResetTrigger(InHash);
        animator.SetTrigger(OutHash);
    }
}