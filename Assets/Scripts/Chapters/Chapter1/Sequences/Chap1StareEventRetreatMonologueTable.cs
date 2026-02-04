using UnityEngine;

public class Chap1StareEventRetreatMonologueTable : MonoBehaviour {
    [Header("Retreat Monologues (Index = Discovery Order)")]
    [TextArea(2, 6)][SerializeField] string[] retreatMonologues;

    public int Count => retreatMonologues != null ? retreatMonologues.Length : 0;

    public bool TryGet(int index, out string msg) {
        msg = null;

        if (retreatMonologues == null)
            return false;

        if (index < 0 || index >= retreatMonologues.Length)
            return false;

        msg = retreatMonologues[index];
        if (string.IsNullOrEmpty(msg))
            return false;

        return true;
    }
}