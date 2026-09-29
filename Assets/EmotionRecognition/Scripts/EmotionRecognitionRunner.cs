/* This code was produced by following the Homuler tutorial:
* https://github.com/homuler/MediaPipeUnityPlugin/blob/master/docs/Tutorial-Task-API.md 
*/


using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Experimental;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEngine;

public enum Emotion
{
    NEUTRAL = 0,
    HAPPY,
    ANGRY,
    SURPRISED,
    UNKNOWN,
    
}

public class EmotionRecognitionRunner : MonoBehaviour
{
    [SerializeField] private int width = 1080;
    [SerializeField] private int height = 720;
    [SerializeField] private int fps = 30;
    [SerializeField] TextAsset modelAsset;
    [SerializeField] EmotionVisualizer visualizer;
    [SerializeField] private EmotionUIBars uiBars = null;
    [SerializeField] private bool useMachineLearning;
    bool trainModel = false;

    WebCamTexture webCamTexture;
    FaceLandmarker faceLandmarker;
    TextureFrame textureFrame;
    Emotion currentEmotion;
    ClassificationModel classificationModel;

    IEnumerator Start()
    {
        // Init
        classificationModel = new ClassificationModel(trainModel, modelAsset);

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

            currentEmotion = detectEmotionHardcode(blendshapes);

            if (useMachineLearning && currentEmotion == Emotion.UNKNOWN)
            {
                currentEmotion = detectEmotionMachineLearning(blendshapes);
            }

            visualizer.UpdateVisualizer(result.faceLandmarks[0].landmarks, currentEmotion);
            EmotionBridge.SetEmotion(currentEmotion);
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

    private Emotion detectEmotionHardcodeOld(List<Category> blendshapes)
    {
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
            return Emotion.HAPPY;
        }
        else if (jawOpen > 0.3f || browUp > 0.5f)
        {
            return Emotion.SURPRISED;
        }
        else if (browDown > 0.5f)
        {
            return Emotion.ANGRY;
        }
        else if (smile < 0.4 && jawOpen < 0.4 && browUp < 0.4 && browDown < 0.4)
        {
            return Emotion.NEUTRAL;
        }
        else
        {
            return Emotion.UNKNOWN;
        }
    }

    private Emotion detectEmotionMachineLearning(List<Category> blendshapes)
    {
        // Recognize emotion
        float[] emotionValues = classificationModel.getEmotions(blendshapes);

        if(uiBars != null) uiBars.UpdateEmotionProbabilities(emotionValues);

        int emotion = 0;
        int counter = 0;
        for (int i = 1; i < emotionValues.Length; i++)
        {
            if (emotionValues[i] > 0.15)
            {
                counter++;
                if (emotionValues[i] > 0.5)
                {
                    emotion = i;
                }
            }
        }

        if(counter > 1)
        {
            return Emotion.NEUTRAL;
        }
        else
        {
            return (Emotion)emotion;
        }
    }

    private Emotion detectEmotionHardcode(List<Category> blendshapes)
    {
        // Extract Blendshapes
        float smileLeft = GetBlendshapeValue(blendshapes, "mouthSmileLeft");
        float smileRight = GetBlendshapeValue(blendshapes, "mouthSmileRight");
        float mouthFrownLeft = GetBlendshapeValue(blendshapes, "mouthFrownLeft");
        float mouthFrownRight = GetBlendshapeValue(blendshapes, "mouthFrownRight");
        float jawOpen = GetBlendshapeValue(blendshapes, "jawOpen");

        float eyeSquintLeft = GetBlendshapeValue(blendshapes, "eyeSquintLeft");
        float eyeSquintRight = GetBlendshapeValue(blendshapes, "eyeSquintRight");
        float eyeWideLeft = GetBlendshapeValue(blendshapes, "eyeWideLeft");
        float eyeWideRight = GetBlendshapeValue(blendshapes, "eyeWideRight");

        float browDownLeft = GetBlendshapeValue(blendshapes, "browDownLeft");
        float browDownRight = GetBlendshapeValue(blendshapes, "browDownRight");
        float browInnerUp = GetBlendshapeValue(blendshapes, "browInnerUp");
        float browOuterUpLeft = GetBlendshapeValue(blendshapes, "browOuterUpLeft");
        float browOuterUpRight = GetBlendshapeValue(blendshapes, "browOuterUpRight");
        float noseSneerLeft = GetBlendshapeValue(blendshapes, "noseSneerLeft");
        float noseSneerRight = GetBlendshapeValue(blendshapes, "noseSneerRight");

        // Averages
        float smile = (smileLeft + smileRight) / 2.0f;
        float mouthFrown = (mouthFrownLeft + mouthFrownRight) / 2.0f;
        float eyeSquint = (eyeSquintLeft + eyeSquintRight) / 2.0f;
        float eyeWide = (eyeWideLeft + eyeWideRight) / 2.0f;
        float browDown = (browDownLeft + browDownRight) / 2.0f;
        float browOuterUp = (browOuterUpLeft + browOuterUpRight) / 2.0f;
        float noseSneer = (noseSneerLeft + noseSneerRight) / 2.0f;

        // Emotion Recognition
        if (smile > 0.45f && browDown < 0.45f && (jawOpen < 0.5f))
        {
            return Emotion.HAPPY;
        }
        else if (browDown > 0.45f && (noseSneer > 0.2f || eyeSquint > 0.25f) && smile < 0.2f)
        {
            return Emotion.ANGRY;
        }
        else if (((eyeWide > 0.2f || browOuterUp > 0.2f || browInnerUp > 0.2f) && (jawOpen > 0.2f)) || (jawOpen > 0.4f))
        {
            return Emotion.SURPRISED;
        }
        else if (smile < 0.25f && browDown < 0.3f && browInnerUp < 0.3f && eyeWide < 0.25f && mouthFrown < 0.2f)
        {
            return Emotion.NEUTRAL;
        }
        else
        {
            return Emotion.UNKNOWN;
        }
    }
}
