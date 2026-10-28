using System.Collections.Generic;
using UnityEngine;

public class ObserverCamera : MonoBehaviour
{
    List<DisplayInfo> displayInfos = new List<DisplayInfo>();
    [SerializeField] Camera observerCamera;

    void Start()
    {
        //DisplayInfo.GetLayout(displayInfos); // I dont' really know if this info is needed for anything. I don't think so?

        Display.displays[1].Activate();
        observerCamera.targetDisplay = 1;   

    }
}
