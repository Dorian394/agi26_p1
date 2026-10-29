using SRD.Core;
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
    [SerializeField] SRDManager SRDisplayManager;
    SRD2DView _srd2DView;
    
    
    [SerializeField]
    private Camera _srdFixedCamera;
    private RenderTexture _fixedCameraTexture;
    private SRDCameras _srdCameras;


    void Start()
    {
        if (useSpatialRealityDisplay)
        {
            _srdCameras = new SRDCameras(SRDisplayManager);
            mainCamera.SetActive(false);
            SRDisplayManager.gameObject.SetActive(true);
            if (SRDisplayManager.Init2DView())
            {
                _srd2DView = SRDisplayManager.SRD2DView;
                _srd2DView.Show(true);
                _srd2DView.SetSourceTexture(SRD2DView.SRDTextureType.Custom);

                //_srd2DView.SetFullScreen(true);
                _srdFixedCamera.CopyFrom(_srdCameras.LeftEyeCamera);
                _srdFixedCamera.targetTexture = _fixedCameraTexture;
                SRDisplayManager.SRD2DView.CustomTexture = _fixedCameraTexture;
                SRDisplayManager.SRD2DView.SetSourceTexture(SRD2DView.SRDTextureType.Custom);
            }
            
        }
        else
        {
            mainCamera.SetActive(true);
            SRDisplayManager.gameObject.SetActive(false);
        }
    }
}
