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
    private static bool needUpdate = false;

    private void Start()
    {
        instance = this;
        if(needUpdate) UpdateGameOverScreen();
    }

    public static void UpdateGameOverScreen()
    {
        needUpdate = true;
        if (!instance) return;
        if (instance.scoreTextMesh != null)
        {
            instance.scoreTextMesh.SetText(ScoreManager.getScore().ToString());
        }
        if (instance.startScene != null)
        {
            needUpdate = false;
            instance.Invoke("RestartGame", 20f);
        }
    }

    private void RestartGame()
    {
        SceneManager.LoadScene(startScene.name);
    } 
}
