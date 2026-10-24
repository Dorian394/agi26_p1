using System.Collections;
using UnityEngine;

public class EmotionBridge
{
    private static Emotion playerEmotion;
    private static Gesture playerGestureLeft;
    private static Gesture playerGestureRight;

    public static Emotion GetEmotion()
    {
        return(playerEmotion);
    }
    
    public static Gesture GetLeftHand()
    {
        return(playerGestureLeft);
    }
    public static Gesture GetRightHand()
    {
        return(playerGestureRight);
    }

    public static void SetEmotion(Emotion emotion)
    {
        playerEmotion = emotion;
    }

    public static void SetLeftHand(Gesture gesture)
    {
        playerGestureLeft = gesture;
    }

    public static void SetRightHand(Gesture gesture)
    {
        playerGestureRight = gesture;
    }
}

