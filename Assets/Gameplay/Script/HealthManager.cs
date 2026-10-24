using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

public class HealthManager : MonoBehaviour
{

    [SerializeField] private float maxHealth = 100;
    [SerializeField] private Image healthBarFill = null;
    [SerializeField] public float damageRadius = 1f;
    [SerializeField] private GameObject gameOverPanel;
    private float health;

    void Start()
    {
        Restart();
    }

    public bool InflictDamage(float damage)
    {
        health -= damage;
        if (health < 0f)
        {
            health = 0f;
            ShowGameOverPanel(true);
            return true;
        }
            
        UpdateHealthBar();
        return (false);
    }

    private void UpdateHealthBar()
    {
        healthBarFill.fillAmount = health / maxHealth;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(this.transform.position, damageRadius);
    }

    public void Restart()
    {
        health = maxHealth;
        UpdateHealthBar();
        ShowGameOverPanel(false);
    }

    public void ShowGameOverPanel(bool display)
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(display);
    }
}
