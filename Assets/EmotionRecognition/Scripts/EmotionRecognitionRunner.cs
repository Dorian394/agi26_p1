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
using Debug = UnityEngine.Debug;

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
    [SerializeField] private int camera_id;
    [SerializeField] private EmotionUIBars uiBars = null;
    [SerializeField] private bool useHardcodedThresholds;
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
            throw new System.Exception("No Web Camera devices found.");
        }
        if (WebCamTexture.devices.Length < camera_id+1)
        {
            throw new System.Exception("Specified Web Camera device not found. Check Camera ID and connection.");
        }
        var webCamDevice = WebCamTexture.devices[camera_id];
        //for (int i = 0; i < WebCamTexture.devices.Length; i++)   // If having trouble with selecting camera, you can use this loop along with the device name to manually select
        //{
        //    print(WebCamTexture.devices[i].name);
        //    if (WebCamTexture.devices[i].name == "Logi C270 HD WebCam") {
        //        webCamDevice = WebCamTexture.devices[i];
        //    }
        //}
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

            currentEmotion = Emotion.UNKNOWN;

            if (useHardcodedThresholds && currentEmotion == Emotion.UNKNOWN)
            {
                currentEmotion = detectEmotionHardcode(blendshapes);
            }

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
        // FACS-inspired facial expression recognition.
        float mouthSmileLeft = GetBlendshapeValue(blendshapes, "mouthSmileLeft");
        float mouthSmileRight = GetBlendshapeValue(blendshapes, "mouthSmileRight");

        float mouthFrownLeft = GetBlendshapeValue(blendshapes, "mouthFrownLeft");
        float mouthFrownRight = GetBlendshapeValue(blendshapes, "mouthFrownRight");

        float mouthPressLeft = GetBlendshapeValue(blendshapes, "mouthPressLeft");
        float mouthPressRight = GetBlendshapeValue(blendshapes, "mouthPressRight");

        float cheekSquintLeft = GetBlendshapeValue(blendshapes, "cheekSquintLeft");
        float cheekSquintRight = GetBlendshapeValue(blendshapes, "cheekSquintRight");

        float mouthShrugLower = GetBlendshapeValue(blendshapes, "mouthShrugLower");

        float browDownLeft = GetBlendshapeValue(blendshapes, "browDownLeft");
        float browDownRight = GetBlendshapeValue(blendshapes, "browDownRight");

        float browInnerUp = GetBlendshapeValue(blendshapes, "browInnerUp");

        float browOuterUpLeft = GetBlendshapeValue(blendshapes, "browOuterUpLeft");
        float browOuterUpRight = GetBlendshapeValue(blendshapes, "browOuterUpRight");

        float jawOpen = GetBlendshapeValue(blendshapes, "jawOpen");

        float eyeWideLeft = GetBlendshapeValue(blendshapes, "eyeWideLeft");
        float eyeWideRight = GetBlendshapeValue(blendshapes, "eyeWideRight");

        float noseSneerLeft = GetBlendshapeValue(blendshapes, "noseSneerLeft");
        float noseSneerRight = GetBlendshapeValue(blendshapes, "noseSneerRight");


        // Average 

        float smile = (mouthSmileLeft + mouthSmileRight) * 0.5f;

        float frown = (mouthFrownLeft + mouthFrownRight) * 0.5f;

        float cheekSquint = (cheekSquintLeft + cheekSquintRight) * 0.5f;

        float browDown = (browDownLeft + browDownRight) * 0.5f;

        float browOuterUp = (browOuterUpLeft + browOuterUpRight) * 0.5f;

        float mouthPress = (mouthPressLeft + mouthPressRight) * 0.5f;

        float eyeWide = (eyeWideLeft + eyeWideRight) * 0.5f;

        float noseSneer = (noseSneerLeft + noseSneerRight) * 0.5f;


        // HAPPY
        float happyScore =
              0.45f * smile
            + 0.40f * cheekSquint;


        // SURPRISED
        float surpriseScore =
              0.25f * browInnerUp
            + 0.20f * browOuterUp
            + 0.30f * eyeWide
            + 0.45f * jawOpen;


        // ANGRY
        float angryScore =
              0.30f * browDown
            + 0.25f * mouthPress
            + 0.30f * mouthShrugLower
            + 0.20f * noseSneer
            + 0.15f * frown;


        // Neutral
        float expressiveActivity =
              0.25f * browDown
            + 0.25f * happyScore
            + 0.25f * surpriseScore
            + 0.20f * angryScore
            + 0.20f * frown
            + 0.05f * mouthPress;


        // ------------------------------------------------------------
        // Select strongest expression
        // ------------------------------------------------------------

        float neutralScore = 0.4f - expressiveActivity;
        float unknownScore = 1.9f * expressiveActivity;

        // Find maximum score.

        float maxScore = neutralScore;
        Emotion detectedEmotion = Emotion.NEUTRAL;

        if (happyScore > maxScore)
        {
            maxScore = happyScore;
            detectedEmotion = Emotion.HAPPY;
        }

        if (surpriseScore > maxScore)
        {
            maxScore = surpriseScore;
            detectedEmotion = Emotion.SURPRISED;
        }

        if (angryScore > maxScore)
        {
            maxScore = angryScore;
            detectedEmotion = Emotion.ANGRY;
        }

        if(unknownScore > maxScore)
        {
            maxScore = unknownScore;
            detectedEmotion = Emotion.UNKNOWN;
        }

        return detectedEmotion;
    }
}
