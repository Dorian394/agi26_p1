using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UserInterfaceManager : MonoBehaviour
{
    [Header("Health")]
    private float defaultHealthBarWidth;
    [SerializeField] private GameObject healthBar;

    [Header("Game Over")]
    [SerializeField] private SceneAsset gameOverScene;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        restart();
        defaultHealthBarWidth = healthBar.transform.localScale.x;
    }

    public void setHealthBar(float size)
    {
        Vector3 scale = healthBar.transform.localScale;
        float clampedSize = Mathf.Clamp01(size);
        healthBar.transform.localScale = new Vector3(defaultHealthBarWidth * clampedSize, scale.y, scale.z);
        if(size <= 0f) healthBar.SetActive(false);
    }

    public void setGameOverScreen(bool state)
    {
        SceneManager.LoadScene(gameOverScene.name);
        GameOverScript.UpdateGameOverScreen();
    }

    public void restart()
    {
        healthBar.SetActive(true);
    }
}
