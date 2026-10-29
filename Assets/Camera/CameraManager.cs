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
    [SerializeField] private RenderTexture _fixedCameraTexture;
    private SRDCameras _srdCameras;
    
    private static CameraManager _instance;

    
    void Awake()
    {
        if (!useSpatialRealityDisplay)
            return;

        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }


    void Start()
    {
        if (!useSpatialRealityDisplay)
        {
            if (mainCamera != null)
                mainCamera.SetActive(true);

            if (SRDisplayManager != null)
                SRDisplayManager.gameObject.SetActive(false);

            return;
        }

        if (mainCamera == null ||
            SRDisplayManager == null ||
            _srdFixedCamera == null ||
            _fixedCameraTexture == null)
        {
            Debug.LogError(
                "CameraManager: Assign Main Camera, SRDisplayManager, " +
                "SRD Fixed Camera, and Fixed Camera Texture in the Inspector."
            );
            return;
        }

        mainCamera.SetActive(false);
        SRDisplayManager.gameObject.SetActive(true);

        // Make sure the texture's graphics resource exists.
        if (!_fixedCameraTexture.IsCreated() &&
            !_fixedCameraTexture.Create())
        {
            Debug.LogError("CameraManager: Failed to create RenderTexture.");
            return;
        }

        // Initialize camera references after activating the manager.
        _srdCameras = new SRDCameras(SRDisplayManager);

        if (!SRDisplayManager.Init2DView())
        {
            Debug.LogError("CameraManager: Init2DView() failed.");
            return;
        }

        _srd2DView = SRDisplayManager.SRD2DView;

        if (_srd2DView == null || _srdCameras.LeftEyeCamera == null)
        {
            Debug.LogError("CameraManager: SRD view or left-eye camera is null.");
            return;
        }

        _srdFixedCamera.CopyFrom(_srdCameras.LeftEyeCamera);
        _srdFixedCamera.targetTexture = _fixedCameraTexture;
        _srdFixedCamera.enabled = true;

        // Assign the valid texture before selecting it as the source.
        _srd2DView.CustomTexture = _fixedCameraTexture;
        _srd2DView.SetSourceTexture(SRD2DView.SRDTextureType.Custom);
        _srd2DView.Show(true);
    }
}
