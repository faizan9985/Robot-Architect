using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PuzzleManager : MonoBehaviour
{
    private readonly HashSet<PuzzlePiece> pieces = new HashSet<PuzzlePiece>();
    private bool missingPiece;

    public int PieceCount => pieces.Count;
    public bool IsCompleted { get; private set; }
    public event System.Action PuzzleCompleted;

    public void SetPieces(IEnumerable<PuzzlePiece> generatedPieces)
    {
        ClearPieces();
        foreach (var piece in generatedPieces)
        {
            if (piece == null || !pieces.Add(piece))
                continue;
            piece.Completed += OnPieceCompleted;
            piece.Removed += OnPieceRemoved;
        }
        CheckCompletion();
    }

    public void ClearPieces()
    {
        foreach (var piece in pieces)
        {
            if (piece == null)
                continue;
            piece.Completed -= OnPieceCompleted;
            piece.Removed -= OnPieceRemoved;
        }
        pieces.Clear();
        IsCompleted = false;
        missingPiece = false;
    }

    private void OnPieceCompleted(PuzzlePiece piece) => CheckCompletion();

    private void OnPieceRemoved(PuzzlePiece piece)
    {
        piece.Completed -= OnPieceCompleted;
        piece.Removed -= OnPieceRemoved;
        pieces.Remove(piece);
        // Destruction must not count as solving a piece. Generate a fresh puzzle to recover.
        missingPiece = true;
    }

    private void CheckCompletion()
    {
        if (IsCompleted || missingPiece || pieces.Count == 0)
            return;
        foreach (var piece in pieces)
        {
            if (!piece.IsCompleted)
                return;
        }
        IsCompleted = true;
        Debug.Log("Puzzle Complete", this);
        PuzzleCompleted?.Invoke();
    }

    private void OnDestroy() => ClearPieces();
}
