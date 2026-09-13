/* This code was produced by following the Homuler tutorial:
* https://github.com/homuler/MediaPipeUnityPlugin/blob/master/docs/Tutorial-Task-API.md 
*/


using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Experimental;
using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;

public enum Emotion
{
    NEUTRAL = 0,
    HAPPY,
    ANGRY,
    SURPRISED
}

public class EmotionRecognitionRunner : MonoBehaviour
{
    [SerializeField] private int width = 1080;
    [SerializeField] private int height = 720;
    [SerializeField] private int fps = 30;
    [SerializeField] TextAsset modelAsset;
    [SerializeField] EmotionVisualizer visualizer;

    WebCamTexture webCamTexture;
    FaceLandmarker faceLandmarker;
    TextureFrame textureFrame;
    Emotion currentEmotion;

    IEnumerator Start()
    {
        // Init
        yield return CreateWebCamTexture();
        textureFrame = new TextureFrame(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32);

        CreateFaceLandmarkerTask();

        var waitForEndOfFrame = new WaitForEndOfFrame();

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        
        // Loop
        while (true)
        {
            ProcessFrame(stopwatch);

            yield return waitForEndOfFrame;
        }
    }


    IEnumerator CreateWebCamTexture()
    {
        if (WebCamTexture.devices.Length == 0)
        {
            throw new System.Exception("Web Camera devices are not found");
        }
        var webCamDevice = WebCamTexture.devices[0];
        webCamTexture = new WebCamTexture(webCamDevice.name, width, height, fps);
        webCamTexture.Play();

        // NOTE: On macOS, the contents of webCamTexture may not be readable immediately, so wait until it is readable
        yield return new WaitUntil(() => webCamTexture.width > 16);
    }

    void CreateFaceLandmarkerTask()
    {
        var options = new FaceLandmarkerOptions(
            baseOptions: new Mediapipe.Tasks.Core.BaseOptions(
                Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU,
                modelAssetBuffer: modelAsset.bytes
            ),
            runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM,
            outputFaceBlendshapes: true,
            resultCallback: OnFaceLandmarkerResult
        );

        faceLandmarker = FaceLandmarker.CreateFromOptions(options);
    }

    void ProcessFrame(Stopwatch stopwatch)
    {
        textureFrame.ReadTextureOnCPU(webCamTexture, flipHorizontally: true, flipVertically: true);
        using var image = textureFrame.BuildCPUImage();
        faceLandmarker.DetectAsync(image, stopwatch.ElapsedMilliseconds);
    }

    // Emotion classification
    private void OnFaceLandmarkerResult(FaceLandmarkerResult result, Mediapipe.Image image, long timestamp)
    {
        if (result.faceLandmarks != null && result.faceBlendshapes.Count > 0)
        {
            var blendshapes = result.faceBlendshapes[0].categories;

            // Get some blendshape values
            float smileLeft = GetBlendshapeValue(blendshapes, "mouthSmileLeft");
            float smileRight = GetBlendshapeValue(blendshapes, "mouthSmileRight");
            float jawOpen = GetBlendshapeValue(blendshapes, "jawOpen");
            float browDownLeft = GetBlendshapeValue(blendshapes, "browDownLeft");
            float browDownRight = GetBlendshapeValue(blendshapes, "browDownRight");
            float browInnerUp = GetBlendshapeValue(blendshapes, "browInnerUp");
            float browOuterUpLeft = GetBlendshapeValue(blendshapes, "browOuterUpLeft");
            float browOuterUpRight = GetBlendshapeValue(blendshapes, "browOuterUpRight");

            // Average
            float browDown = (browDownLeft + browDownRight) / 2.0f;
            float smile = (smileLeft + smileRight) / 2.0f;
            float browUp = (browInnerUp + browOuterUpLeft + browOuterUpRight) / 3.0f;

            // Recognize emotion
            if (smile > 0.6f)
            {
                currentEmotion = Emotion.HAPPY;
            }
            else if (jawOpen > 0.3f || browUp > 0.5f)
            {
                currentEmotion = Emotion.SURPRISED;
            }
            else if (browDown > 0.3f)
            {
                currentEmotion = Emotion.ANGRY;
            }
            else
            {
                currentEmotion = Emotion.NEUTRAL;
            }

            visualizer.UpdateVisualizer(result.faceLandmarks[0].landmarks, currentEmotion);
        }
    }

    private float GetBlendshapeValue(System.Collections.Generic.List<Mediapipe.Tasks.Components.Containers.Category> categories, string name)
    {
        foreach (var category in categories)
        {
            if (category.categoryName == name) return category.score;
        }
        return 0f;
    }


    private void OnDestroy()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
        }
        textureFrame?.Release();
        faceLandmarker?.Close();
    }

}
