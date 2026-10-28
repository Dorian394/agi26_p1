using System;
using System.Collections.Specialized;
using TMPro;
using UnityEngine;

public class ScoreDisplayer : MonoBehaviour
{
    [SerializeField] private TextMeshPro scoreTMP;
    string defaultValue = "0000000"; 

    void Start()
    {
        scoreTMP.text = defaultValue;
        ScoreManager.registerScoreDisplayer(this);
    }

    public void SetScore(long score)
    {
        scoreTMP.text = score.ToString(defaultValue);
    }
}
