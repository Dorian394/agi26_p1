using System.Collections.Generic;
using System.Diagnostics;
using Mediapipe;
using UnityEngine;
using UnityEngine.UI;

using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.HolisticLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Experimental;
using MediaPipeXRHands;
using Debug = UnityEngine.Debug;
using RunningMode = Mediapipe.Tasks.Vision.Core.RunningMode;

public class CameraSelectionAttribute : PropertyAttribute
{
}

public class HolisticTracker : MonoBehaviour
{
    [Header("Visualizers")]
    [SerializeField] private EmotionRecognitionRunner emotions;
    [SerializeField] private MediaPipeXRHandAdapter xrHandAdapter;
    
    [Header("UI")]
    [SerializeField] private RawImage screen;
    
    [Header("Model")]
    [SerializeField] private TextAsset handLandmarkerModel;
    [SerializeField] private TextAsset faceLandmarkerModel;
    
    [Header("Camera")]
    [SerializeField] private int width = 1080;
    [SerializeField] private int height = 720;
    [SerializeField] private int fps = 30;
    [SerializeField] [CameraSelection] private int cameraId;


    private WebCamTexture webcam;
    private HandLandmarker handLandmarker;
    private FaceLandmarker faceLandmarker;

    private TextureFrame textureFrame;
    private Stopwatch stopwatch;

    private void Start()
    {
        StartCoroutine(Run());
    }

    private System.Collections.IEnumerator Run()
    {
        // --------------------------------------------------
        // 1. Start webcam
        // --------------------------------------------------

        webcam = new WebCamTexture(width, height, fps);
        webcam.Play();

        yield return new WaitUntil(() =>
            webcam.width > 16 &&
            webcam.height > 16
        );

        screen.texture = webcam;
        screen.SetNativeSize();

        Debug.Log(
            $"Webcam started: {webcam.width} x {webcam.height}"
        );

        // --------------------------------------------------
        // 2. Create HolisticLandmarker
        // --------------------------------------------------

        
        var handLandmarkerOptions = new HandLandmarkerOptions(
            new BaseOptions(
                modelAssetBuffer: handLandmarkerModel.bytes
            ),
            runningMode: RunningMode.LIVE_STREAM,
            
            
            numHands:2,
            
            resultCallback: xrHandAdapter.UpdateResult
        );

        var faceLandmarkerOptions = new FaceLandmarkerOptions(
            new BaseOptions(
                modelAssetBuffer: faceLandmarkerModel.bytes
            ),
            runningMode: RunningMode.LIVE_STREAM,
            
            numFaces:1,
            
            outputFaceBlendshapes: true,
            resultCallback: emotions.OnFaceLandmarkerResult
        );
        
        handLandmarker =
            HandLandmarker.CreateFromOptions(handLandmarkerOptions);

        faceLandmarker =
            FaceLandmarker.CreateFromOptions(faceLandmarkerOptions);

        Debug.Log("HolisticLandmarker created.");

        // --------------------------------------------------
        // 3. Prepare texture frame
        // --------------------------------------------------

        textureFrame = new TextureFrame(
            webcam.width,
            webcam.height,
            TextureFormat.RGBA32
        );
        
        stopwatch = new Stopwatch();
        stopwatch.Start(); 
        
        var waitForEndOfFrame =
            new WaitForEndOfFrame();


        while (true)
        {
            yield return waitForEndOfFrame;

            // Unity's texture coordinate system is different
            // from MediaPipe's, so flip vertically.
            textureFrame.ReadTextureOnCPU(
                webcam,
                flipHorizontally: true,
                flipVertically: true
            );

            var timestamp = stopwatch.ElapsedMilliseconds;
            faceLandmarker.DetectAsync(textureFrame.BuildCPUImage(), timestamp);
            handLandmarker.DetectAsync(textureFrame.BuildCPUImage(), timestamp);
        }
    }
    

    private void OnDestroy()
    {
        if (webcam != null)
        {
            webcam.Stop();
        }

        textureFrame?.Dispose();

        handLandmarker?.Close();
        handLandmarker = null;
        
        faceLandmarker?.Close();
        faceLandmarker = null;
    }
}
