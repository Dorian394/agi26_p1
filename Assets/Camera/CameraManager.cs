using UnityEngine;

/*
 * This class just has a toggle for whether to use the main camera or the display manager.
 * 
 * It could be put in its own folder, but given that it's just a single script and no other objects, it felt unnecessary. If you disagree, feel free to move it.
 */

public class CameraManager : MonoBehaviour
{
    [SerializeField] private bool useSpatialRealityDisplay;
    [SerializeField] GameObject mainCamera;
    [SerializeField] GameObject SRDisplayManager;

    void Start()
    {
        if (useSpatialRealityDisplay)
        {
            mainCamera.SetActive(false);
            SRDisplayManager.SetActive(true);
        }
        else
        {
            mainCamera.SetActive(true);
            SRDisplayManager.SetActive(false);
        }
    }
}
