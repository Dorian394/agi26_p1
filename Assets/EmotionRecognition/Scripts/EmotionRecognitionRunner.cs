/* This code was produced by following the Homuler tutorial:
* https://github.com/homuler/MediaPipeUnityPlugin/blob/master/docs/Tutorial-Task-API.md 
*/


using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Experimental;
using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using Mediapipe;
using Mediapipe.Tasks.Vision.HolisticLandmarker;
using UnityEngine;
using Debug = UnityEngine.Debug;

public enum Emotion
{
    NEUTRAL = 0,
    HAPPY,
    ANGRY,
    SURPRISED
}

public class EmotionRecognitionRunner : MonoBehaviour
{
    [SerializeField] EmotionVisualizer visualizer;

    WebCamTexture webCamTexture;
    FaceLandmarker faceLandmarker;
    TextureFrame textureFrame;
    Emotion currentEmotion;

    // Emotion classification
    public void OnFaceLandmarkerResult(FaceLandmarkerResult result, Image image, long timestampMillisec)
    {
        if (result.faceLandmarks != null && result.faceLandmarks.Count > 0 &&
            result.faceLandmarks[0].landmarks != null && result.faceLandmarks[0].landmarks.Count > 0 &&
            result.faceBlendshapes != null && result.faceBlendshapes.Count > 0 &&
            result.faceBlendshapes[0].categories != null && result.faceBlendshapes[0].categories.Count > 0)
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
            
            float mouthFrownLeft = GetBlendshapeValue(blendshapes, "mouthFrownLeft");
            float mouthFrownRight = GetBlendshapeValue(blendshapes, "mouthFrownRight");

            // Average
            float browDown = (browDownLeft + browDownRight) / 2.0f;
            float smile = (smileLeft + smileRight) / 2.0f;
            float browUp = (browInnerUp + browOuterUpLeft + browOuterUpRight) / 3.0f;
            float mouthFrown =  (mouthFrownLeft + mouthFrownRight) / 2.0f;
            
            
            // Recognize emotion
            if (smile > 0.6f)
            {
                currentEmotion = Emotion.HAPPY;
            }
            else if (jawOpen > 0.3f || browUp > 0.5f)
            {
                currentEmotion = Emotion.SURPRISED;
            }
            else if (browDown > 0.5f || mouthFrown > 0.5f)
            {
                currentEmotion = Emotion.ANGRY;
            }
            else
            {
                currentEmotion = Emotion.NEUTRAL;
            }

            float aspect = (image != null && image.Height() > 0)
                ? (float)image.Width() / image.Height()
                : -1f;

            visualizer.UpdateVisualizer(result.faceLandmarks[0].landmarks, currentEmotion, aspect);
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
    
}
