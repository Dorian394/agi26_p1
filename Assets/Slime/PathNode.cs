using System;
using UnityEngine;

public class PathNode : MonoBehaviour
{
    [field: SerializeField] public PathNode Parent {get; private set;}
    public double parentDistance {get; private set;}
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (Parent != null)
        {
            parentDistance = Vector3.Distance(transform.position, Parent.transform.position);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDrawGizmos()
    {
        if (Parent == null)
        {
            return;
        }
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, Parent.transform.position);
    }
}
