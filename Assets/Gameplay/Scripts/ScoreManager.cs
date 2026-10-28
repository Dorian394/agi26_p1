using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private static long score;
    private static List<ScoreDisplayer> displayers = new List<ScoreDisplayer>();
    
    void Start()
    {
        restart();
    }

    public static void restart()
    {
        score = 0;
        notifyScoreDisplayers();
    }

    public static void addScore(long value)
    {
        score += value;
        notifyScoreDisplayers();
    }

    public static long getScore()
    {
        return score;
    }

    public static void registerScoreDisplayer(ScoreDisplayer scoreDisplayer)
    {
        displayers.Add(scoreDisplayer);
    }

    private static void notifyScoreDisplayers()
    {
        foreach (var displayer in displayers)
        {
            displayer.SetScore(score);
        }
    }
}
