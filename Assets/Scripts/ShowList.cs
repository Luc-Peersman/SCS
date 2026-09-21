using System.Collections.Generic;
using UnityEngine;
using static ShowControl;

public class ShowList : MonoBehaviour
{
    [Header("Geluiden (geef elk geluid een naam en sleep er een AudioClip op)")]
    public List<ShowControl.Geluid> geluiden = new List<ShowControl.Geluid>();

    void Start()
    {
        Cue.SpeelGeluidOpUitgangen(1f, "StereoTest", Kant.RECHTS, 4);
        Cue.SpeelGeluidOpUitgangen(1f, "StereoTest", Kant.BEIDE, 3);
    }
}
