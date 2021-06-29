using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR.Extras;                                          // SteamVR_LaserPoint를 받아오기 위해서 선언함.
public class PointHandler : MonoBehaviour
{
    public SteamVR_LaserPointer laserPointer;

    void Start()
    {
        // 대리자 (Delegate 연결)
        laserPointer.PointerIn += PointerInside;
        laserPointer.PointerOut += PointerOutside;
        laserPointer.PointerClick += PointerClick;
    }

    public void PointerInside(object sender, PointerEventArgs eventArgs)
    {
        if (eventArgs.target.CompareTag("UI"))
        {
            Debug.Log("UI Inside");
        }
    }
    public void PointerOutside(object sender, PointerEventArgs eventArgs)
    {
        if (eventArgs.target.CompareTag("UI"))
        {
            Debug.Log("UI Inside");
        }
    }
    public void PointerClick(object sender, PointerEventArgs eventArgs)
    {
        if(eventArgs.target.CompareTag("UI"))
        {
            Debug.Log(eventArgs.target.name);
        }
    }
}
