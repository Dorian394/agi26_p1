using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverScript : MonoBehaviour
{
    [SerializeField] private TextMeshPro scoreTextMesh;
    [SerializeField] private SceneAsset startScene;
    private static GameOverScript instance;

    private void Start()
    {
        instance = this;
        UpdateGameOverScreen();
    }

    public static void UpdateGameOverScreen()
    {
        if (!instance) return;
        if (instance.scoreTextMesh != null)
        {
            instance.scoreTextMesh.SetText(ScoreManager.getScore().ToString());
        }
        if (instance.startScene != null)
        {
            instance.Invoke("RestartGame", 3f);
        }
    }

    private void RestartGame()
    {
        SceneManager.LoadScene(startScene.name);
    } 
}
