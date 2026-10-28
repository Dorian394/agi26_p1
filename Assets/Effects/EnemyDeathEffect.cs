using System;
using UnityEngine;

public class EnemyDeathEffect : MonoBehaviour
{
    [Header("Audio")]
    AudioSource audioSource;
    [SerializeField] AudioClip[] audioClips;
    [Header("Particles")]
    [SerializeField] GameObject[] particleObjects;
    [Header("Lifetime")]
    private float endTime;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        endTime = Time.time + 1f; // Default value   
    }

    public void Initialize(Emotion emotion)
    {
        // Audio
        audioSource = GetComponent<AudioSource>();
        AudioClip clip = audioClips[UnityEngine.Random.Range(0, audioClips.Length)];
        audioSource.PlayOneShot(clip);
        float lifetime = clip.length;

        // Particles
        switch (emotion)
        {
            case Emotion.HAPPY: 
                particleObjects[0].SetActive(true);
                lifetime = Math.Max(lifetime, 6f);
                break;
            case Emotion.ANGRY: 
                particleObjects[1].SetActive(true);
                lifetime = Math.Max(lifetime, 10f);
                break;
            case Emotion.SURPRISED: 
                particleObjects[2].SetActive(true);
                lifetime = Math.Max(lifetime, 4f);
                break;
        }
        endTime = Time.time + lifetime;
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time >= endTime)
        {
            // We self-destruct to clear memory.
            Destroy(gameObject);
        }
    }
}
