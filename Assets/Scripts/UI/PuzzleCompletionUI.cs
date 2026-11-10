using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleCompletionUI : MonoBehaviour
{
    [SerializeField] private PuzzleManager puzzleManager;
    [SerializeField] private PuzzleGenerator puzzleGenerator;
    [SerializeField] private Camera xrCamera;
    [SerializeField] private Canvas completionCanvas;
    [SerializeField] private Button newPuzzleButton;
    [SerializeField] private Button retryPuzzleButton;
    [SerializeField] private ConfettiUI confetti;
    [SerializeField, Min(0.5f)] private float distance = 1.2f;
    [SerializeField] private float verticalOffset = -0.05f;
    private bool visible;

    private void OnEnable()
    {
        puzzleManager.PuzzleCompleted += Show;
        puzzleGenerator.PuzzleSpawned += Hide;
        newPuzzleButton.onClick.AddListener(NewPuzzle);
        retryPuzzleButton.onClick.AddListener(RetryPuzzle);
        Hide();
        if (puzzleManager.IsCompleted)
            Show();
    }

    private void OnDisable()
    {
        puzzleManager.PuzzleCompleted -= Show;
        puzzleGenerator.PuzzleSpawned -= Hide;
        newPuzzleButton.onClick.RemoveListener(NewPuzzle);
        retryPuzzleButton.onClick.RemoveListener(RetryPuzzle);
        Hide();
    }

    private void Show()
    {
        if (visible || xrCamera == null)
            return;
        var forward = Vector3.ProjectOnPlane(xrCamera.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(xrCamera.transform.up, Vector3.up);
        forward.Normalize();
        completionCanvas.transform.SetPositionAndRotation(
            xrCamera.transform.position + forward * distance + Vector3.up * verticalOffset,
            Quaternion.LookRotation(forward, Vector3.up));
        completionCanvas.worldCamera = xrCamera;
        visible = true;
        completionCanvas.gameObject.SetActive(true);
        confetti.Play();
    }

    private void Hide()
    {
        visible = false;
        confetti.StopAndReset();
        completionCanvas.gameObject.SetActive(false);
    }

    private void NewPuzzle()
    {
        if (!visible) return;
        Hide();
        puzzleGenerator.GenerateNewPuzzle();
    }

    private void RetryPuzzle()
    {
        if (!visible) return;
        Hide();
        puzzleGenerator.RetryCurrentPuzzle();
    }
}
