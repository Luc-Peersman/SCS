using System.Collections.Generic;
using UnityEngine;
using static ShowControl;

public class ShowList : MonoBehaviour
{
    [Header("Geluiden (geef elk geluid een naam en sleep er een AudioClip op)")]
    public List<ShowControl.Geluid> geluiden = new List<ShowControl.Geluid>();

    void Start()
    {
        // Plaats hier je Cues
        Cue.SocketAan(2f,3);
        Cue.SpeelGeluid(3f,3, "StereoTest", Kant.RECHTS);
       Cue.SocketUit(6f,3);
    }
}
