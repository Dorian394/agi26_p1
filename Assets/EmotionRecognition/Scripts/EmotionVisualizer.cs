using System.Collections.Generic;
using System.Linq;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EmotionVisualizer : MonoBehaviour
{
    [Header("Mesh Properties")]
    [SerializeField] private Mesh sourceMesh; // Mesh used for dynamic feedback

    [Header("Emotion Colors")]
    [SerializeField] private Color happyColor = Color.lightYellow;
    [SerializeField] private Color surprisedColor = Color.lavender;
    [SerializeField] private Color angryColor = Color.softRed;
    [SerializeField] private Color unknownColor = Color.softGreen;
    [SerializeField] private Color defaultColor = Color.lightGray;
    
    [Header("Attack")]
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private AudioSource chargeSfx;
    [SerializeField] private AudioSource shootSfx;


    // Object-related fields
    Material faceMaterial; // Mesh renderer
    private Mesh faceMesh; // Mesh used to store a copy of the input mesh
    private Vector3[] vertices; // List of vertices used to compute the new mesh

    // Bridge with the Runner script
    private List<NormalizedLandmark> landmarkListTemp; // Temp buffer used to receive the new Landmarks
    private List<NormalizedLandmark> currentLandmarks;
    private bool hasNewData = false; // Flag set to true when new landmarks are available
    private readonly object lockObj = new(); // Lock to synchronize the accesses to the shared array landmarkListTemp
    
    private readonly Queue<(float time, Emotion emotion)> emotions = new();
    private Emotion lastEmotion = Emotion.UNKNOWN;
    private float chargeTime;
    private bool shooted;
    private Vector3 baseScale;
    private float maxChargeScale = 1.5f;
    
    

    void Start()
    {
        if (sourceMesh == null)
        {
            Debug.LogError("Error: Source mesh is empty");
            return;
        }

        // Init the lists
        landmarkListTemp = new();
        currentLandmarks = new();

        // Create a copy of the input mesh and apply it to the object
        faceMesh = new Mesh
        {
            name = "FaceMesh",
            vertices = sourceMesh.vertices,
            triangles = sourceMesh.triangles,
            uv = sourceMesh.uv
        };
        faceMesh.MarkDynamic();
        vertices = new Vector3[faceMesh.vertexCount];
        GetComponent<MeshFilter>().mesh = faceMesh;

        // Get the mesh renderer for future updates
        faceMaterial= GetComponent<MeshRenderer>().material;
        
        baseScale = transform.localScale;

    }

    public void UpdateVisualizer(List<NormalizedLandmark> receivedLandmarkList, Emotion emotion)
    {
        if (receivedLandmarkList == null || receivedLandmarkList.Count == 0) return;

        // Ensure mutual exclusion
        lock (lockObj)
        {
            // Copy the lists (not just the reference)
            landmarkListTemp.Clear();
            landmarkListTemp.AddRange(receivedLandmarkList);


            hasNewData = true;
        }
    }

    private void Update()
    {
        if (!hasNewData || faceMesh == null) return;

        // Ensure mutual exclusion
        lock (lockObj)
        {
            currentLandmarks.Clear();
            currentLandmarks.AddRange(landmarkListTemp);
        }
        hasNewData = false;

        if (currentLandmarks == null) return;

        int count = Mathf.Min(currentLandmarks.Count, vertices.Length);

        // Update Geometry
        for (int i = 0; i < count; i++)
        {
            NormalizedLandmark lm = currentLandmarks[i];

            float x = -(0.5f - lm.x);
            float y = (0.5f - lm.y);
            float z = lm.z;

            vertices[i] = new Vector3(x, y, z);
        }

        faceMesh.vertices = vertices;
        faceMesh.RecalculateNormals();
        faceMesh.RecalculateBounds();

        Emotion emotion = EmotionBridge.GetEmotion();

        // Add sample
        emotions.Enqueue((Time.time, emotion));

        // Remove samples older than 0.2s
        while (emotions.Count > 0 && Time.time - emotions.Peek().time > 0.2f)
            emotions.Dequeue();

        // Rolling majority
        var counts = new Dictionary<Emotion, int>();

        foreach (var sample in emotions)
            counts[sample.emotion] = counts.GetValueOrDefault(sample.emotion) + 1;

        Emotion smoothEmotion = counts
            .OrderByDescending(x => x.Value)
            .First().Key;

        // Update texture
        var color = smoothEmotion switch
        {
            Emotion.HAPPY => happyColor,
            Emotion.ANGRY => angryColor,
            Emotion.SURPRISED => surprisedColor,
            _ => defaultColor,
        };

        faceMaterial.SetColor("_BaseColor", color);

        // Charge
        if (smoothEmotion != lastEmotion)
        {
            lastEmotion = smoothEmotion;
            chargeTime = 0f;
            shooted = false;
            chargeSfx.Stop();
            transform.localScale = baseScale;
        }
        else if (smoothEmotion != Emotion.NEUTRAL && smoothEmotion != Emotion.UNKNOWN)
        {
            chargeTime += Time.deltaTime;
            
            float charge = Mathf.Clamp01(chargeTime / 1f);
            transform.localScale = baseScale * Mathf.Lerp(1f, maxChargeScale, charge);


            if (!shooted && chargeTime >= 0.3f && !chargeSfx.isPlaying)
            {
                chargeSfx.Play();
            }
            if (!shooted && chargeTime >= 1f)
            {
                chargeSfx.Stop();
                if (enemyManager.Shoot(smoothEmotion))
                {
                    shooted = true;
                    shootSfx.Play();
                }
            }
        }
    }

}
