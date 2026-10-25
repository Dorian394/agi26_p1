/* This code was produced by following the Homuler tutorial:
* https://github.com/homuler/MediaPipeUnityPlugin/blob/master/docs/Tutorial-Task-API.md 
*/


using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Experimental;
using System.Collections.Generic;
using Mediapipe;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using MediaPipeXRHands;
using UnityEngine;
using Debug = UnityEngine.Debug;

public enum Emotion
{
    NEUTRAL = 0,
    HAPPY = 1,
    ANGRY = 2,
    SURPRISED = 3,
    SAD = 4,
    UNKNOWN = -1,
    
}

public enum Gesture
{
    THUMB_UP = 0,
    OPEN_PALM,
    CLOSED_FIST,
    VICTORY,
    NONE
}

public class EmotionRecognitionRunner : MonoBehaviour
{
    [SerializeField] TextAsset modelAsset;
    [SerializeField] EmotionVisualizer visualizer;
    [SerializeField] private EmotionUIBars uiBars = null;
    [SerializeField] private bool useHardcodedThresholds;
    [SerializeField] private bool useMachineLearning;
    bool trainModel = false;
    private readonly HandednessDisambiguator handDisambiguator = new HandednessDisambiguator();
    
    FaceLandmarker faceLandmarker;
    TextureFrame textureFrame;
    Emotion currentEmotion;
    ClassificationModel classificationModel;
    private void Start()
    {
        // Init
        classificationModel = new ClassificationModel(trainModel, modelAsset);
    }
    
    // Emotion classification
    public void OnFaceLandmarkerResult(FaceLandmarkerResult result, Image image, long timestampMillisec)
    {
        if (result.faceLandmarks != null && result.faceLandmarks.Count > 0 &&
            result.faceLandmarks[0].landmarks != null && result.faceLandmarks[0].landmarks.Count > 0 &&
            result.faceBlendshapes != null && result.faceBlendshapes.Count > 0 &&
            result.faceBlendshapes[0].categories != null && result.faceBlendshapes[0].categories.Count > 0)
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

    private void OnDestroy()
    {
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
            return Emotion.HAPPY;
        }
        else if (jawOpen > 0.3f || browUp > 0.5f)
        {
            return Emotion.SURPRISED;
        }
        else if (browDown > 0.5f || mouthFrown > 0.5f)
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
    
    private float GetBlendshapeValue(List<Category> categories, string name)
    {
        foreach (var category in categories)
        {
            if (category.categoryName == name) return category.score;
        }
        return 0f;
    }

    public void OnHandGestureResult(GestureRecognizerResult gestureRecognizerResult, Image image,
        long timestampMillisec)
    {
        var (leftIndex, rightIndex) = handDisambiguator.Disambiguate(gestureRecognizerResult);
        if (leftIndex != -1)
        {
            Debug.Log("Left hand:" + gestureRecognizerResult.gestures[leftIndex].categories[0]);
            EmotionBridge.SetLeftHand(GetGesture(gestureRecognizerResult.gestures[leftIndex].categories[0]));
        }

        if (rightIndex != -1){
            Debug.Log("Right hand:" + gestureRecognizerResult.gestures[rightIndex].categories[0]);
            EmotionBridge.SetRightHand(GetGesture(gestureRecognizerResult.gestures[rightIndex].categories[0]));
        } 
    }

    private Gesture GetGesture(Category category)
    {
        return category.categoryName switch
        {
            "Thumb_Up" => Gesture.THUMB_UP,
            "Open_Palm" => Gesture.OPEN_PALM,
            "Closed_Fist" => Gesture.CLOSED_FIST,
            "Victory" => Gesture.VICTORY,
            _ => Gesture.NONE
        };
    }
}
