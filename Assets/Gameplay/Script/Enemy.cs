using System;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    // Fields

    HealthManager target;
    public Emotion emotion;
    float speed;
    MeshRenderer childMeshRenderer;
    GameObject deathEffect;
    private bool isDead = false;

    public void Initialize(HealthManager target, float speed, Emotion emotion, GameObject deathEffect)
    {
        this.target = target;
        this.emotion = emotion;
        this.speed = speed;
        this.deathEffect = deathEffect;
        this.isDead = false;


        // Update texture
        childMeshRenderer = GetComponentInChildren<MeshRenderer>();
        var color = emotion switch
        {
            Emotion.HAPPY => Color.lightYellow,
            Emotion.ANGRY => Color.softRed,
            Emotion.SURPRISED => Color.pink,
            _ => Color.lightGray,
        };
        childMeshRenderer.material.SetColor("_BaseColor", color);
        
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        agent.speed = speed;
        agent.destination = this.target.transform.position; 
    }

    private void Update()
    {
        if (target != null)
        {
            float distance = Vector3.Distance(this.transform.position, target.transform.position);
            if(distance < target.damageRadius)
            {
                target.InflictDamage(10f);
                this.Kill();
            }
        }
    }

    public void Kill()
    {
        if (!isDead)
        {
            isDead = true;
            SpawnDeathEffect();
            Destroy(gameObject);
            
        }
    }

    private void SpawnDeathEffect()
    {
        if (deathEffect == null) return;

        GameObject newEffectObj = Instantiate(deathEffect, this.transform.position, Quaternion.identity);
    }
}
