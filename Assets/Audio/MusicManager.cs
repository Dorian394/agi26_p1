using System.Linq;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    // Based on https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/audiosource/playscheduled
    // Some sections are directly copied
    
    public float bpm = 98.0f;
    public int numBeatsPerSegment = 16;
    
    private double nextEventTime;
    private bool running = false;
    private int flip = 0;
    private int clipIndex = 0;
    
    [SerializeField] AudioClip[] musicClips;
    private AudioSource[] audioSources = new AudioSource[2];
    
    void Start()
    {
        for (int i = 0; i < 2; i++)
        {
            GameObject child = new GameObject("Player");
            child.transform.parent = gameObject.transform;
            audioSources[i] = child.AddComponent<AudioSource>();
        }

        nextEventTime = AudioSettings.dspTime + 2.0f;
        running = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!running)
        {
            return;
        }
        
        double time = AudioSettings.dspTime;

        if (time + 1.0f > nextEventTime)
        {
            // We are now approx. 1 second before the time at which the sound should play,
            // so we will schedule it now in order for the system to have enough time
            // to prepare the playback at the specified time. This may involve opening
            // buffering a streamed file and should therefore take any worst-case delay into account.
            audioSources[flip].clip = musicClips[clipIndex];
            audioSources[flip].PlayScheduled(nextEventTime);
            
            // Place the next event 16 beats from here at a rate of 140 beats per minute
            nextEventTime += 60.0f / bpm * numBeatsPerSegment;
            
            clipIndex = (clipIndex + 1) % musicClips.Length;
            flip = 1 - flip;
        }
    }
}
