using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private static long score;   
    
    void Start()
    {
        restart();
    }

    public static void restart()
    {
        score = 0;
    }

    public static void addScore(long value)
    {
        score += value;
    }

    public static long getScore()
    {
        return score;
    }
}
