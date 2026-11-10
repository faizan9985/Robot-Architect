using UnityEngine;

[DisallowMultipleComponent]
public sealed class PuzzleTarget : MonoBehaviour
{
    [SerializeField] private PuzzlePieceType acceptedType;
    [SerializeField] private Renderer markerRenderer;
    private PuzzlePiece matchingPiece;
    private bool paired;
    private bool occupied;

    public PuzzlePiece MatchingPiece => matchingPiece;

    public void Initialize(PuzzlePiece piece, Color color)
    {
        matchingPiece = piece;
        paired = true;
        acceptedType = piece.PieceType;
        occupied = false;
        var properties = new MaterialPropertyBlock();
        markerRenderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        markerRenderer.SetPropertyBlock(properties);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (occupied)
            return;

        var piece = other.GetComponentInParent<PuzzlePiece>();
        if (piece != null && piece.PieceType == acceptedType &&
            (!paired || piece == matchingPiece))
            occupied = piece.TryLockTo(transform);
    }
}
