using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Experimental;
using MediaPipeXRHands;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using RunningMode = Mediapipe.Tasks.Vision.Core.RunningMode;

public class CameraSelectionAttribute : PropertyAttribute
{
}

public class HolisticTracker : MonoBehaviour
{
    [Header("Visualizers")] [SerializeField]
    private EmotionRecognitionRunner emotions;

    [SerializeField] private MediaPipeXRHandAdapter xrHandAdapter;

    [Header("UI")] [SerializeField] private RawImage screen;

    [Header("Model")] [SerializeField] private TextAsset handLandmarkerModel;

    [SerializeField] private TextAsset handGesturesModel;
    [SerializeField] private TextAsset faceLandmarkerModel;

    [Header("Camera")] [SerializeField] private int width = 1080;

    [SerializeField] private int height = 720;
    [SerializeField] private int fps = 30;
    [SerializeField] [CameraSelection] private int cameraId;
    private FaceLandmarker faceLandmarker;
    private GestureRecognizer handGestureRecognizer;
    private HandLandmarker handLandmarker;
    private Stopwatch stopwatch;

    private TextureFrame textureFrame;


    private WebCamTexture webcam;

    private void Start()
    {
        StartCoroutine(Run());
    }


    private void OnDestroy()
    {
        if (webcam != null) webcam.Stop();

        textureFrame?.Dispose();

        handLandmarker?.Close();
        handLandmarker = null;

        handGestureRecognizer.Close();
        handGestureRecognizer = null;

        faceLandmarker?.Close();
        faceLandmarker = null;
    }

    private IEnumerator Run()
    {
        // 1. Start webcam

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

        // 2. Create mediapipe models runners

        var handLandmarkerOptions = new HandLandmarkerOptions(
            new BaseOptions(
                modelAssetBuffer: handLandmarkerModel.bytes
            ),
            RunningMode.LIVE_STREAM,
            2,
            resultCallback: xrHandAdapter.UpdateResult
        );
        
        var gestureClassifierOptions = new ClassifierOptions();
        gestureClassifierOptions.categoryAllowlist = new List<string>
        {
            "Thumb_Up",
            "Open_Palm",
            "Closed_Fist",
            "Victory"
        };
        gestureClassifierOptions.maxResults = 1;
        gestureClassifierOptions.scoreThreshold = 0.5f;

        var handGestureRecognizerOptions = new GestureRecognizerOptions(
            new BaseOptions(
                modelAssetBuffer: handGesturesModel.bytes
            ),
            RunningMode.LIVE_STREAM,
            numHands: 2,
            cannedGestureClassifierOptions: gestureClassifierOptions,
            resultCallback: emotions.OnHandGestureResult
        );

        var faceLandmarkerOptions = new FaceLandmarkerOptions(
            new BaseOptions(
                modelAssetBuffer: faceLandmarkerModel.bytes
            ),
            RunningMode.LIVE_STREAM,
            outputFaceBlendshapes: true,
            resultCallback: emotions.OnFaceLandmarkerResult
        );

        handLandmarker =
            HandLandmarker.CreateFromOptions(handLandmarkerOptions);

        handGestureRecognizer =
            GestureRecognizer.CreateFromOptions(handGestureRecognizerOptions);

        faceLandmarker =
            FaceLandmarker.CreateFromOptions(faceLandmarkerOptions);

        Debug.Log("HolisticLandmarker created.");

        // 3. Prepare texture frame

        textureFrame = new TextureFrame(
            webcam.width,
            webcam.height,
            TextureFormat.RGBA32
        );

        stopwatch = new Stopwatch();
        stopwatch.Start();

        var waitForEndOfFrame =
            new WaitForEndOfFrame();

        // 4. Run models
        
        while (true)
        {
            yield return waitForEndOfFrame;

            // Unity's texture coordinate system is different
            // from MediaPipe's, so flip vertically.
            textureFrame.ReadTextureOnCPU(
                webcam,
                true,
                true
            );

            var timestamp = stopwatch.ElapsedMilliseconds;
            faceLandmarker.DetectAsync(textureFrame.BuildCPUImage(), timestamp);
            handLandmarker.DetectAsync(textureFrame.BuildCPUImage(), timestamp);
        }
    }
}
