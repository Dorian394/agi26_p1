using System.Collections;
using UnityEngine;

public class EmotionBridge
{
    private static Emotion playerEmotion;

    public static Emotion GetEmotion()
    {
        return(playerEmotion);
    }

    public static void SetEmotion(Emotion emotion)
    {
        playerEmotion = emotion;
    }
}

