using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

public class Slime : MonoBehaviour
{
    private PathNode target;
    public Emotion emotion;
    float speed;
    SlimeManager manager;
    private NavMeshAgent agent;
    private float animParamter = 0.0f;
    private float animSpeed = 0.75f; // radians per second? Or degrees per second? idk
    private float animOffset;

    public void Initialize(PathNode target, float speed, Emotion emotion, SlimeManager manager)
    {
        this.target = target;
        this.emotion = emotion;
        this.speed = speed;
        this.manager = manager;
        animOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
    }

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = speed;
        agent.destination = this.target.transform.position; 
    }

    void Update()
    {
        var swapDistance = 1.0;
        var distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
        if (distanceToTarget < swapDistance)
        {   
            NextNode();
        }
        else if (target.Parent != null)
        {
            var distanceToParent = Vector3.Distance(transform.position, target.Parent.transform.position);
            if (distanceToParent < target.parentDistance)
            {
                NextNode();
            }
        }

        var verticalScale =   (float)(1.125 + Math.Sin(animOffset + animParamter)     * 0.25);
        var horizontalScale = (float)(1.1   - Math.Sin(animOffset + animParamter + 0.1) * 0.2);
        animParamter += Time.deltaTime * animSpeed * speed;
        transform.localScale = new Vector3(horizontalScale, verticalScale, horizontalScale);
    }

    void NextNode()
    {
        if (target.Parent == null)
        {
            manager.InflictDamages();
            manager.DeleteClosestEnemy();
            return; // TODO call some kind of function for reaching destination.
        }
        target = target.Parent;
        agent.destination = target.transform.position;
    }
    
}
