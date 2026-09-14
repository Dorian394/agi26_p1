using UnityEngine;

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
    }

    
    void Update()
    {
        if (target == null) return;

        Vector3 targetPosition = new(target.transform.position.x, transform.position.y, target.transform.position.z);

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        Vector3 direction = targetPosition - transform.position;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
