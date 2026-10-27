using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private int score;   
    
    void Start()
    {
        restart();
    }

    public void restart()
    {
        score = 0;
    }

    public void addScore(int value)
    {
        score += value;
    }
}
