using System;
using UnityEngine;

public class EnemyDeathEffect : MonoBehaviour
{
    /*
     * Currently, this only handles the sound effect. 
     * You may add more effects if you want, either as components of the same object, or as children.
     * If you add other types of effects as children, you may want to refactor the audio source as a child object, as well.
     */

    AudioSource audioSource;
    [SerializeField] AudioClip[] audioClips;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.PlayOneShot(audioClips[UnityEngine.Random.Range(0,audioClips.Length)]);
    }

    // Update is called once per frame
    void Update()
    {
        if (!audioSource.isPlaying)
        {
            // We self-destruct to clear memory.
            Destroy(gameObject);
        }
    }
}
