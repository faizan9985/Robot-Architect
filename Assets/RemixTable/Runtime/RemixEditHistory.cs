using System.Collections.Generic;
using UnityEngine;

namespace RemixTable
{
    // Undo / redo for ribbon edits. One step = one pinch hold, from pinch to release: the automation is
    // snapshotted when a hold begins and the snapshot is kept only if the hold actually changed something.
    // History is per song session and is cleared when the song changes.
    public sealed class RemixEditHistory
    {
        public const int MaxSteps = 20;
        readonly List<List<AutomationEdit>> undo = new List<List<AutomationEdit>>(), redo = new List<List<AutomationEdit>>();
        List<AutomationEdit> pending;
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public int UndoCount => undo.Count;
        public int RedoCount => redo.Count;
        public bool GestureOpen => pending != null;
        // Bumped on every change so UI can refresh cheaply.
        public int Version { get; private set; }

        public void BeginGesture(AutomationStore store) => pending = Snapshot(store);
        // Returns true when the finished hold was recorded as an undo step.
        public bool EndGesture(AutomationStore store)
        {
            if (pending == null) return false;
            var before = pending; pending = null;
            if (Same(before, store.edits)) return false;
            Push(undo, before);
            redo.Clear(); Version++;
            return true;
        }
        public bool Undo(AutomationStore store) => Step(store, undo, redo);
        public bool Redo(AutomationStore store) => Step(store, redo, undo);
        public void Clear() { undo.Clear(); redo.Clear(); pending = null; Version++; }

        bool Step(AutomationStore store, List<List<AutomationEdit>> from, List<List<AutomationEdit>> to)
        {
            if (from.Count == 0) return false;
            pending = null;
            Push(to, Snapshot(store));
            var state = from[from.Count - 1]; from.RemoveAt(from.Count - 1);
            store.edits.Clear();
            foreach (var e in state) store.edits.Add(Clone(e)); // Stored snapshots stay immutable.
            Version++;
            return true;
        }
        static void Push(List<List<AutomationEdit>> stack, List<AutomationEdit> state)
        {
            stack.Add(state);
            if (stack.Count > MaxSteps) stack.RemoveAt(0);
        }
        static List<AutomationEdit> Snapshot(AutomationStore store)
        {
            var copy = new List<AutomationEdit>(store.edits.Count);
            foreach (var e in store.edits) copy.Add(Clone(e));
            return copy;
        }
        static AutomationEdit Clone(AutomationEdit e) => new AutomationEdit
        {
            id = e.id, songId = e.songId, stem = e.stem, start = e.start, end = e.end, value = e.value,
            parameter = e.parameter, wholeStem = e.wholeStem, curve = e.curve == null ? null : new AnimationCurve(e.curve.keys)
        };
        static bool Same(List<AutomationEdit> a, List<AutomationEdit> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                var x = a[i]; var y = b[i];
                if (x.id != y.id || x.songId != y.songId || x.stem != y.stem || x.parameter != y.parameter || x.wholeStem != y.wholeStem ||
                    x.start != y.start || x.end != y.end || x.value != y.value) return false;
                if ((x.curve == null) != (y.curve == null)) return false;
                if (x.curve != null)
                {
                    var kx = x.curve.keys; var ky = y.curve.keys;
                    if (kx.Length != ky.Length) return false;
                    for (int k = 0; k < kx.Length; k++) if (kx[k].time != ky[k].time || kx[k].value != ky[k].value) return false;
                }
            }
            return true;
        }
    }
}
