using Unity.VisualScripting;
using UnityEngine;

public class HealthManager : MonoBehaviour
{
    [Header("Health settings")]
    private float health;
    [SerializeField] private float maxHealth = 100;
    [Header("UI")]
    [SerializeField] UserInterfaceManager userInterfaceManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        restart();
    }

    public bool inflictDamages(float damages)
    {
        health -= damages;
        userInterfaceManager.setHealthBar(health / maxHealth);

        if (health < 0)
        {
            health = 0;
            userInterfaceManager.setGameOverScreen(true);
            return true;
        }
        else
        {
            return false;
        }

    }

    public void restart()
    {
        health = maxHealth;
    }
}
