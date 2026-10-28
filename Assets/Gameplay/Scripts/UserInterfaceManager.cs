using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UserInterfaceManager : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private GameObject healthBar;
    [SerializeField] private TextMeshPro healthText;
    private float defaultHealthBarWidth;

    [Header("Game Over")]
    [SerializeField] private SceneAsset gameOverScene;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        restart();
        defaultHealthBarWidth = healthBar.transform.localScale.x;
    }

    public void setHealthBarSize(float size)
    {
        // Set health bar size
        Vector3 scale = healthBar.transform.localScale;
        float clampedSize = Mathf.Clamp01(size);
        healthBar.transform.localScale = new Vector3(defaultHealthBarWidth * clampedSize, scale.y, scale.z);
        if(size <= 0f) healthBar.SetActive(false);
    }

    public void setHealthBarText(float hp, float maxHp)
    {
        int iHp = (int)Mathf.Ceil(hp);
        int iMaxHp = (int)Mathf.Ceil(maxHp);

        string text = iHp.ToString() + "/" + iMaxHp.ToString();
        healthText.SetText(text);
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
