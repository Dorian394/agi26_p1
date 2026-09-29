using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    GameObject target;
    public Emotion emotion;
    float speed;
    Vector3 targetPosition;
    MeshRenderer childMeshRenderer;

    public void Initialize(GameObject target, float speed, Emotion emotion)
    {
        this.target = target;
        this.emotion = emotion;
        this.speed = speed;


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
}
