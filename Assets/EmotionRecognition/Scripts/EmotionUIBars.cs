using UnityEngine;
using UnityEngine.UI;

public class EmotionUIBars : MonoBehaviour
{
    const int nbEmotions = 4;
    [Header("UI Bars")]
    [SerializeField] private Image[] emotionBars = new Image[4];


    private float[] targetProbabilities = new float[nbEmotions];

    void Update()
    {
        for (int i = 0; i < emotionBars.Length; i++)
        {
            if (emotionBars[i] != null)
            {
                emotionBars[i].fillAmount = targetProbabilities[i];
            }
    }
    }


    public void UpdateEmotionProbabilities(float[] probabilities)
    {
        if (probabilities == null || probabilities.Length != nbEmotions)
        {
            Debug.LogError("Emotion array must contain exactly 5 emotions.");
            return;
        }

        for (int i = 0; i < nbEmotions; i++)
        {
            targetProbabilities[i] = Mathf.Clamp01(probabilities[i]);
        }
    }
}
