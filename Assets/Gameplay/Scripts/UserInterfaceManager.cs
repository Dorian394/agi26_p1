using UnityEngine;

public class UserInterfaceManager : MonoBehaviour
{
    [Header("Health")]
    private float defaultHealthBarWidth;
    [SerializeField] private GameObject healthBar;

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
    }

    public void setGameOverScreen(bool state)
    {
        Debug.Log("GAME OVER");
    }

    public void restart()
    {

    }
}
