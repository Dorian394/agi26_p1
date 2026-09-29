using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EmotionVisualizer : MonoBehaviour
{
    [Header("Mesh Properties")]
    [SerializeField] private Mesh sourceMesh; // Mesh used for dynamic feedback

    [Header("Camera & Space")]
    [Tooltip("The camera reference used to unproject 2D face landmarks into 3D camera space. If null, Camera.main will be used.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("If true, the face is placed in world space matching targetCamera. If false, it is placed in camera local space.")]
    [SerializeField] private bool inWorldSpace = true;

    [Tooltip("Mirrors the X axis horizontally if your camera is not already mirrored (note: HolisticTracker flips horizontally by default).")]
    [SerializeField] private bool mirrorX = false;

    [Header("Scale & Calibration")]
    [Tooltip("Scale multiplier for the face mesh.")]
    [SerializeField] private float worldScale = 1.0f;

    [Tooltip("The real-world distance in meters assumed for the first detected face (calibrates face size).")]
    [SerializeField] private float calibrationDistance = 1.0f;

    [Tooltip("Whether face size has been calibrated. Calibrates automatically on the first detected face.")]
    [SerializeField] private bool isCalibrated = false;

    [Tooltip("Calibrated apparent face scale reference at calibration distance.")]
    [SerializeField] private float calibratedFaceSize = 0f;

    [Header("Depth Bounds & Filtering")]
    [SerializeField] private float minDepth = 0.2f;
    [SerializeField] private float maxDepth = 3.0f;

    [Range(0f, 1f)]
    [Tooltip("Smoothing factor for depth estimation (0 = no smoothing, higher = smoother but more lag).")]
    [SerializeField] private float depthSmoothing = 0.2f;

    [Tooltip("Inverts the relative Z depth of face features.")]
    [SerializeField] private bool invertZ = false;

    [Tooltip("Scale multiplier for the depth relief of facial features.")]
    [SerializeField] private float zFeatureScale = 1.0f;

    [Header("Emotion Colors")]
    [SerializeField] private Color happyColor = Color.lightYellow;
    [SerializeField] private Color surprisedColor = Color.lavender;
    [SerializeField] private Color angryColor = Color.softRed;
    [SerializeField] private Color defaultColor = Color.lightGray;

    // Object-related fields
    private MeshRenderer meshRenderer;
    private Mesh faceMesh;
    private Vector3[] vertices;

    // Bridge with the Runner script (thread-safe buffers)
    private readonly object lockObj = new();
    private bool hasNewData;
    private Emotion pendingEmotion;
    private float pendingAspect = -1f;
    private readonly List<NormalizedLandmark> pendingLandmarks = new();
    private readonly List<NormalizedLandmark> currentLandmarks = new();

    // Depth smoothing state
    private float smoothedDepth = 1.0f;
    private bool wasTracked = false;

    // Cached camera parameters
    private float cachedFovY = 60f;
    private float cachedAspect = 16f / 9f;

    public bool IsCalibrated => isCalibrated;
    public float EstimatedDepth => smoothedDepth;

    [ContextMenu("Recalibrate Face Size")]
    public void Recalibrate()
    {
        isCalibrated = false;
        calibratedFaceSize = 0f;
        wasTracked = false;
        Debug.Log("[EmotionVisualizer] Face recalibration requested.");
    }

    private void Awake()
    {
        transform.localScale = Vector3.one;
    }

    private void Start()
    {
        UpdateCameraCache();

        if (sourceMesh == null)
        {
            Debug.LogError("Error: Source mesh is empty");
            return;
        }

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

        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void Update()
    {
        UpdateCameraCache();

        Emotion emotion;
        float aspect;

        lock (lockObj)
        {
            if (!hasNewData || !faceMesh)
                return;

            currentLandmarks.Clear();
            currentLandmarks.AddRange(pendingLandmarks);
            emotion = pendingEmotion;
            aspect = pendingAspect > 0f ? pendingAspect : cachedAspect;
            hasNewData = false;
        }

        UpdateFace(currentLandmarks, aspect);
        UpdateColor(emotion);
    }

    private void UpdateCameraCache()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            cachedFovY = targetCamera.fieldOfView;
            cachedAspect = targetCamera.aspect;
        }
    }

    public void UpdateVisualizer(List<NormalizedLandmark> receivedLandmarks, Emotion emotion, float imageAspect = -1f)
    {
        if (receivedLandmarks == null || receivedLandmarks.Count == 0) return;

        lock (lockObj)
        {
            pendingLandmarks.Clear();
            pendingLandmarks.AddRange(receivedLandmarks);
            pendingEmotion = emotion;
            pendingAspect = imageAspect;
            hasNewData = true;
        }
    }

    private void UpdateFace(List<NormalizedLandmark> landmarks, float aspect)
    {
        int count = Mathf.Min(landmarks.Count, vertices.Length);
        if (count < 264)
            return;

        // 1. Face size calibration and depth estimation (perspective 1:1 scale)
        float currentFaceSize = CalculateFaceScreenSize(landmarks, aspect);
        if (!isCalibrated && currentFaceSize > 0.01f)
        {
            calibratedFaceSize = currentFaceSize * calibrationDistance;
            isCalibrated = true;
            Debug.Log($"[EmotionVisualizer] Face calibrated: size = {calibratedFaceSize:F4} (reference distance: {calibrationDistance:F2}m)");
        }

        float rawDepth = (isCalibrated && currentFaceSize > 0.005f)
            ? (calibratedFaceSize / currentFaceSize)
            : calibrationDistance;

        rawDepth = Mathf.Clamp(rawDepth, minDepth, maxDepth);

        if (wasTracked && depthSmoothing > 0f)
        {
            smoothedDepth = Mathf.Lerp(smoothedDepth, rawDepth, 1.0f - depthSmoothing);
        }
        else
        {
            smoothedDepth = rawDepth;
            wasTracked = true;
        }

        float depth = smoothedDepth;

        // 2. Camera frustum projection setup
        float tanHalfFovY = Mathf.Tan(cachedFovY * 0.5f * Mathf.Deg2Rad);
        float tanHalfFovX = tanHalfFovY * aspect;
        float frustumWidth = 2.0f * depth * tanHalfFovX;

        // 3. Anchor center: landmark 168 (rigid bridge of the nose / nasion)
        NormalizedLandmark centerLm = landmarks.Count > 168 ? landmarks[168] : landmarks[0];
        float centerU = mirrorX ? (1.0f - centerLm.x) : centerLm.x;
        float centerV = centerLm.y;
        float centerZOffset = (invertZ ? -centerLm.z : centerLm.z) * frustumWidth * zFeatureScale;
        float centerDepth = depth + centerZOffset;

        float centerX = (centerU - 0.5f) * 2.0f * centerDepth * tanHalfFovX;
        float centerY = (0.5f - centerV) * 2.0f * centerDepth * tanHalfFovY;
        float centerZ = centerDepth;
        Vector3 faceCenterCam = new Vector3(centerX, centerY, centerZ);

        // 4. Compute 3D metric camera-space positions for all landmarks, centered around faceCenterCam
        for (int i = 0; i < count; i++)
        {
            NormalizedLandmark lm = landmarks[i];

            float u = mirrorX ? (1.0f - lm.x) : lm.x;
            float v = lm.y;
            float zOffset = (invertZ ? -lm.z : lm.z) * frustumWidth * zFeatureScale;
            float lmDepth = depth + zOffset;

            float x = (u - 0.5f) * 2.0f * lmDepth * tanHalfFovX;
            float y = (0.5f - v) * 2.0f * lmDepth * tanHalfFovY;
            float z = lmDepth;

            Vector3 lmCamPos = new Vector3(x, y, z);
            vertices[i] = (lmCamPos - faceCenterCam) * worldScale;
        }

        // 5. Position and orient the GameObject
        if (inWorldSpace && targetCamera != null)
        {
            transform.position = targetCamera.transform.TransformPoint(faceCenterCam);
            transform.rotation = targetCamera.transform.rotation;
        }
        else
        {
            transform.localPosition = faceCenterCam;
            transform.localRotation = Quaternion.identity;
        }
        transform.localScale = Vector3.one;

        // 6. Update mesh geometry
        faceMesh.vertices = vertices;
        faceMesh.RecalculateNormals();
        faceMesh.RecalculateBounds();
    }

    private float CalculateFaceScreenSize(List<NormalizedLandmark> landmarks, float aspect)
    {
        if (landmarks == null || landmarks.Count < 455)
            return 0f;

        // Rigid skull landmarks invariant to facial expressions:
        // Outer eye corners: 33 (left) and 263 (right)
        float dEyes = Dist3D(landmarks[33], landmarks[263], aspect);

        // Temples / zygomatic width: 234 (left) and 454 (right)
        float dTemples = Dist3D(landmarks[234], landmarks[454], aspect);

        // Forehead to nasion: 10 (forehead) and 168 (nasion)
        float dForehead = Dist3D(landmarks[10], landmarks[168], aspect);

        // Inner eye corners: 133 and 362
        float dInnerEyes = Dist3D(landmarks[133], landmarks[362], aspect);

        return (dEyes + dTemples + dForehead + dInnerEyes) * 0.25f;
    }

    private static float Dist3D(NormalizedLandmark a, NormalizedLandmark b, float aspect)
    {
        float dx = (a.x - b.x) * aspect;
        float dy = a.y - b.y;
        float dz = (a.z - b.z) * aspect;
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private void UpdateColor(Emotion emotion)
    {
        var color = emotion switch
        {
            Emotion.HAPPY => happyColor,
            Emotion.ANGRY => angryColor,
            Emotion.SURPRISED => surprisedColor,
            _ => defaultColor,
        };
        meshRenderer.material.SetColor("_BaseColor", color);
    }
}
