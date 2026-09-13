using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EmotionVisualizer : MonoBehaviour
{
    [Header("Mesh Properties")]
    [SerializeField] private Mesh sourceMesh; // Mesh used for dynamic feedback
    [SerializeField] private float scaleFactor = 10f;

    [Header("Emotion Colors")]
    [SerializeField] private Color happyColor = Color.lightYellow;
    [SerializeField] private Color surprisedColor = Color.lavender;
    [SerializeField] private Color angryColor = Color.softRed;
    [SerializeField] private Color defaultColor = Color.lightGray;

    // Object-related fields
    MeshRenderer meshRenderer; // Mesh renderer
    private Mesh faceMesh; // Mesh used to store a copy of the input mesh
    private Vector3[] vertices; // List of vertices used to compute the new mesh

    // Bridge with the Runner script
    private List<NormalizedLandmark> landmarkListTemp; // Temp buffer used to receive the new Landmarks
    private bool hasNewData = false; // Flag set to true when new landmarks are available
    private readonly object lockObj = new(); // Lock to synchronize the accesses to the shared array landmarkListTemp
    Emotion currentEmotion; // Current emotion


    
    

    void Start()
    {
        if (sourceMesh == null)
        {
            Debug.LogError("Error: Source mesh is empty");
            return;
        }

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
        meshRenderer = GetComponent<MeshRenderer>();
    }

    public void UpdateVisualizer(List<NormalizedLandmark> receivedLandmarkList, Emotion emotion)
    {
        if (receivedLandmarkList == null || receivedLandmarkList.Count == 0) return;

        // Ensure mutual exclusion
        lock (lockObj)
        {
            this.landmarkListTemp = receivedLandmarkList;
            this.currentEmotion = emotion;
            this.hasNewData = true;
        }
    }

    private void Update()
    {
        if (!hasNewData || faceMesh == null) return;

        List<NormalizedLandmark> currentLandmarks = null;

        // Ensure mutual exclusion
        lock (lockObj)
        {
            currentLandmarks = landmarkListTemp;
            hasNewData = false;
        }

        if (currentLandmarks == null) return;

        int count = Mathf.Min(currentLandmarks.Count, vertices.Length);

        // Update Geometry
        for (int i = 0; i < count; i++)
        {
            NormalizedLandmark lm = currentLandmarks[i];

            float x = (0.5f - lm.x) * scaleFactor;
            float y = (0.5f - lm.y) * scaleFactor;
            float z = -lm.z * scaleFactor;

            vertices[i] = new Vector3(x, y, z);
        }

        faceMesh.vertices = vertices;
        faceMesh.RecalculateNormals();
        faceMesh.RecalculateBounds();

        // Update texture
        var color = currentEmotion switch
        {
            Emotion.HAPPY => happyColor,
            Emotion.ANGRY => angryColor,
            Emotion.SURPRISED => surprisedColor,
            _ => defaultColor,
        };
        meshRenderer.material.SetColor("_BaseColor", color);
    }
}
