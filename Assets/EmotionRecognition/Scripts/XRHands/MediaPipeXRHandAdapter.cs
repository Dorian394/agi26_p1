using System;
using Mediapipe;
using UnityEngine;
using UnityEngine.XR.Hands;

using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Color = UnityEngine.Color;
using NormalizedLandmark = Mediapipe.Tasks.Components.Containers.NormalizedLandmark;
using Landmark = Mediapipe.Tasks.Components.Containers.Landmark;
using Rect = UnityEngine.Rect;

namespace MediaPipeXRHands
{
    public sealed class MediaPipeXRHandAdapter : MonoBehaviour
    {
        [Header("Camera & Space")]
        [Tooltip("The camera reference used to unproject 2D hand landmarks into 3D camera space. If null, Camera.main will be used.")]
        [SerializeField]
        private Camera targetCamera;

        [Tooltip("If true, poses are transformed into world space using targetCamera. If false, poses are relative to the camera (camera is origin at 0,0,0).")]
        [SerializeField]
        private bool posesInWorldSpace = false;

        [Tooltip("Mirrors the X axis if your camera is mirrored (selfie/webcam). Note: HolisticTracker already flips horizontally by default.")]
        [SerializeField]
        private bool mirrorX = false;

        [Header("Face Anchor (Optional)")]
        [Tooltip("Optional reference to EmotionVisualizer to anchor hand depth to the player's calibrated face distance.")]
        [SerializeField]
        private EmotionVisualizer faceVisualizer;

        [Tooltip("If true and face visualizer is calibrated, hands calibrate against and are bounded by the face depth.")]
        [SerializeField]
        private bool anchorToFaceDepth = true;

        [Header("Scale & Calibration")]
        [Tooltip("Scale multiplier for the hand bones.")]
        [SerializeField]
        private float worldScale = 1.0f;

        [Tooltip("The real-world distance in meters assumed for the first detected hand (calibrates hand size).")]
        [SerializeField]
        private float calibrationDistance = 1.0f;

        [Tooltip("Whether hand size has been calibrated. Calibrates automatically on the first detected hand.")]
        [SerializeField]
        private bool isCalibrated = false;

        [Tooltip("Calibrated apparent hand scale reference at calibration distance.")]
        [SerializeField]
        private float calibratedHandSize = 0f;

        [Header("Depth Bounds & Filtering")]
        [SerializeField]
        private float minDepth = 0.2f;

        [SerializeField]
        private float maxDepth = 3.0f;

        [Range(0f, 1f)]
        [Tooltip("Smoothing factor for depth estimation (0 = no smoothing, higher = smoother).")]
        [SerializeField]
        private float depthSmoothing = 0.5f;

        [Header("Tracking Continuity")]
        [Tooltip("Seconds to maintain hand tracking after MediaPipe temporarily loses sight of the hand (eliminates rapid dropouts/flashing).")]
        [SerializeField]
        private float trackingLostTimeout = 0.15f;

        [Header("Joint Smoothing")]
        [Range(0f, 1f)]
        [Tooltip("Smoothing factor for hand wrist position (0 = raw, higher = smoother).")]
        [SerializeField]
        private float wristPositionSmoothing = 0.45f;

        [Range(0f, 1f)]
        [Tooltip("Smoothing factor for individual joint positions (0 = raw, higher = smoother).")]
        [SerializeField]
        private float jointPositionSmoothing = 0.4f;

        [Range(0f, 1f)]
        [Tooltip("Smoothing factor for joint rotations (0 = raw, higher = smoother).")]
        [SerializeField]
        private float jointRotationSmoothing = 0.4f;

        [Range(0f, 1f)]
        [Tooltip("Smoothing factor for palm dorsal normal (prevents knuckle roll jitter).")]
        [SerializeField]
        private float normalSmoothing = 0.4f;

        [Header("Debug")]
        [Tooltip("Log tracking updates and jump warnings to the console.")]
        [SerializeField]
        private bool enableDebugLogging = true;

        [Tooltip("Show an on-screen HUD with live tracking coordinates and jump detection.")]
        [SerializeField]
        private bool showOnScreenDebug = true;

        private MediaPipeXRHandSubsystem subsystem;

        // Thread-safe synchronization lock
        private readonly object syncLock = new object();
        private bool hasNewData = false;
        private bool pendingLeftSeen = false;
        private bool pendingRightSeen = false;
        private float pendingImageAspect = -1f;

        // Preallocated landmark staging buffers for background callbacks
        private readonly NormalizedLandmark[] pendingLeftNorm = new NormalizedLandmark[21];
        private readonly Landmark[] pendingLeftWorld = new Landmark[21];
        private readonly NormalizedLandmark[] pendingRightNorm = new NormalizedLandmark[21];
        private readonly Landmark[] pendingRightWorld = new Landmark[21];

        // Main thread working buffers
        private readonly NormalizedLandmark[] currentLeftNorm = new NormalizedLandmark[21];
        private readonly Landmark[] currentLeftWorld = new Landmark[21];
        private readonly NormalizedLandmark[] currentRightNorm = new NormalizedLandmark[21];
        private readonly Landmark[] currentRightWorld = new Landmark[21];

        private readonly Vector3[] leftJointPoints = new Vector3[21];
        private readonly Vector3[] rightJointPoints = new Vector3[21];

        private readonly Pose[] leftRawPoses = new Pose[XRHandJointID.EndMarker.ToIndex()];
        private readonly Pose[] rightRawPoses = new Pose[XRHandJointID.EndMarker.ToIndex()];
        private readonly Pose[] leftSmoothedPoses = new Pose[XRHandJointID.EndMarker.ToIndex()];
        private readonly Pose[] rightSmoothedPoses = new Pose[XRHandJointID.EndMarker.ToIndex()];
        private readonly Pose[] leftOutputPoses = new Pose[XRHandJointID.EndMarker.ToIndex()];
        private readonly Pose[] rightOutputPoses = new Pose[XRHandJointID.EndMarker.ToIndex()];

        private Vector3 prevLeftOutputPos;
        private Vector3 prevRightOutputPos;
        private float lastLeftJump;
        private float lastRightJump;

        // Smoothing and tracking state
        private float leftSmoothedDepth = 1.0f;
        private float rightSmoothedDepth = 1.0f;
        private Vector3 leftSmoothedWristPos = Vector3.zero;
        private Vector3 rightSmoothedWristPos = Vector3.zero;
        private Vector3 leftSmoothedNormal = Vector3.forward;
        private Vector3 rightSmoothedNormal = Vector3.forward;

        private bool leftWasTracked = false;
        private bool rightWasTracked = false;
        private float leftLastSeenTime = -1f;
        private float rightLastSeenTime = -1f;

        // Handedness disambiguation state
        private Vector2 lastLeftWrist2D = new Vector2(0.3f, 0.5f);
        private Vector2 lastRightWrist2D = new Vector2(0.7f, 0.5f);
        private int singleHandFlipStreak = 0;

        private struct RawDetection
        {
            public string category;
            public float score;
            public Vector2 wristNorm;
            public int index;
        }

        // Cached camera parameters
        private float cachedFovY = 60f;
        private float cachedAspect = 16f / 9f;
        private Vector3 cachedCameraPosition = Vector3.zero;
        private Quaternion cachedCameraRotation = Quaternion.identity;

        public bool IsCalibrated => isCalibrated;

        private void Start()
        {
            UpdateCameraCache();

            if (faceVisualizer == null)
            {
                faceVisualizer = FindFirstObjectByType<EmotionVisualizer>();
            }

            for (var i = 0; i < leftSmoothedPoses.Length; i++)
            {
                leftRawPoses[i] = Pose.identity;
                rightRawPoses[i] = Pose.identity;
                leftSmoothedPoses[i] = Pose.identity;
                rightSmoothedPoses[i] = Pose.identity;
                leftOutputPoses[i] = Pose.identity;
                rightOutputPoses[i] = Pose.identity;
            }

            subsystem = MediaPipeXRHandSubsystem.Create();
            if (subsystem == null)
            {
                Debug.LogError("Could not create MediaPipe XR Hand subsystem.");
                return;
            }

            subsystem.Start();
        }

        private void Update()
        {
            UpdateCameraCache();

            bool leftSeen;
            bool rightSeen;
            float aspect;

            // 1. Thread-safe snapshot of pending MediaPipe results
            lock (syncLock)
            {
                if (hasNewData)
                {
                    leftSeen = pendingLeftSeen;
                    rightSeen = pendingRightSeen;
                    aspect = pendingImageAspect > 0f ? pendingImageAspect : cachedAspect;

                    if (leftSeen)
                    {
                        Array.Copy(pendingLeftNorm, currentLeftNorm, 21);
                        Array.Copy(pendingLeftWorld, currentLeftWorld, 21);
                    }

                    if (rightSeen)
                    {
                        Array.Copy(pendingRightNorm, currentRightNorm, 21);
                        Array.Copy(pendingRightWorld, currentRightWorld, 21);
                    }

                    hasNewData = false;
                }
                else
                {
                    leftSeen = false;
                    rightSeen = false;
                    aspect = cachedAspect;
                }
            }

            MediaPipeXRHandProvider.EnableDebugLogging = enableDebugLogging;

            // 2. Process each hand on the Main Thread with smoothing and grace period
            ProcessHand(
                Handedness.Left,
                leftSeen,
                currentLeftNorm,
                currentLeftWorld,
                leftRawPoses,
                leftSmoothedPoses,
                leftOutputPoses,
                leftJointPoints,
                ref prevLeftOutputPos,
                ref lastLeftJump,
                ref leftSmoothedDepth,
                ref leftSmoothedWristPos,
                ref leftSmoothedNormal,
                ref leftWasTracked,
                ref leftLastSeenTime,
                aspect);

            ProcessHand(
                Handedness.Right,
                rightSeen,
                currentRightNorm,
                currentRightWorld,
                rightRawPoses,
                rightSmoothedPoses,
                rightOutputPoses,
                rightJointPoints,
                ref prevRightOutputPos,
                ref lastRightJump,
                ref rightSmoothedDepth,
                ref rightSmoothedWristPos,
                ref rightSmoothedNormal,
                ref rightWasTracked,
                ref rightLastSeenTime,
                aspect);
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
                cachedCameraPosition = targetCamera.transform.position;
                cachedCameraRotation = targetCamera.transform.rotation;
            }
        }

        [ContextMenu("Recalibrate Hand Size")]
        public void Recalibrate()
        {
            isCalibrated = false;
            calibratedHandSize = 0f;
            leftWasTracked = false;
            rightWasTracked = false;
            leftLastSeenTime = -1f;
            rightLastSeenTime = -1f;
            lastLeftWrist2D = new Vector2(0.3f, 0.5f);
            lastRightWrist2D = new Vector2(0.7f, 0.5f);
            singleHandFlipStreak = 0;
            Debug.Log("[MediaPipeXRHandAdapter] Hand calibration reset.");
        }

        /// <summary>
        /// Asynchronous MediaPipe callback (runs on a background thread).
        /// Copies landmark data under lock with handedness disambiguation.
        /// Guaranteed to never assign both slots to the same hand or overwrite.
        /// </summary>
        public void UpdateResult(
            HandLandmarkerResult result, Image image, long timestampMillisec)
        {
            lock (syncLock)
            {
                pendingLeftSeen = false;
                pendingRightSeen = false;

                // 1. Gather all valid hand detections (up to 2)
                RawDetection det0 = default;
                RawDetection det1 = default;
                int validCount = 0;

                if (result.handedness != null && result.handWorldLandmarks != null && result.handLandmarks != null)
                {
                    for (var i = 0; i < result.handedness.Count && validCount < 2; i++)
                    {
                        if (i >= result.handWorldLandmarks.Count || i >= result.handLandmarks.Count)
                            break;

                        if (result.handedness[i].categories == null || result.handedness[i].categories.Count == 0)
                            continue;

                        var normList = result.handLandmarks[i].landmarks;
                        var worldList = result.handWorldLandmarks[i].landmarks;
                        if (normList == null || normList.Count < 21 || worldList == null || worldList.Count < 21)
                            continue;

                        var det = new RawDetection
                        {
                            category = result.handedness[i].categories[0].categoryName,
                            score = result.handedness[i].categories[0].score,
                            wristNorm = new Vector2(normList[0].x, normList[0].y),
                            index = i
                        };

                        if (validCount == 0) det0 = det;
                        else det1 = det;
                        validCount++;
                    }
                }

                // 2. Disambiguate and assign detections to Left and Right
                if (validCount == 1)
                {
                    int predictedHand = (det0.category == "Left") ? 0 : 1;
                    int assignedHand = predictedHand;

                    // If only one hand was previously tracked and this detection is right at its position:
                    // Guard against a single-frame misclassification jump across the screen
                    if (leftWasTracked && !rightWasTracked)
                    {
                        float dLeft = Vector2.Distance(det0.wristNorm, lastLeftWrist2D);
                        if (predictedHand == 1 && dLeft < 0.3f && singleHandFlipStreak < 2)
                        {
                            singleHandFlipStreak++;
                            assignedHand = 0; // Closest wins: keep as Left
                        }
                        else
                        {
                            singleHandFlipStreak = (predictedHand == 1) ? singleHandFlipStreak + 1 : 0;
                            assignedHand = predictedHand;
                        }
                    }
                    else if (rightWasTracked && !leftWasTracked)
                    {
                        float dRight = Vector2.Distance(det0.wristNorm, lastRightWrist2D);
                        if (predictedHand == 0 && dRight < 0.3f && singleHandFlipStreak < 2)
                        {
                            singleHandFlipStreak++;
                            assignedHand = 1; // Closest wins: keep as Right
                        }
                        else
                        {
                            singleHandFlipStreak = (predictedHand == 0) ? singleHandFlipStreak + 1 : 0;
                            assignedHand = predictedHand;
                        }
                    }
                    else
                    {
                        singleHandFlipStreak = 0;
                        assignedHand = predictedHand;
                    }

                    if (assignedHand == 0)
                    {
                        pendingLeftSeen = true;
                        CopyLandmarks(result, det0.index, pendingLeftNorm, pendingLeftWorld);
                    }
                    else
                    {
                        pendingRightSeen = true;
                        CopyLandmarks(result, det0.index, pendingRightNorm, pendingRightWorld);
                    }
                }
                else if (validCount == 2)
                {
                    singleHandFlipStreak = 0;
                    bool det0IsLeft;

                    if (det0.category != det1.category)
                    {
                        // Clear distinct prediction from MediaPipe (one Left, one Right)
                        det0IsLeft = (det0.category == "Left");
                    }
                    else
                    {
                        // Conflict: Both detections were labeled with the SAME handedness!
                        // "the closest wins" - compare total distance to last tracked positions
                        if (leftWasTracked || rightWasTracked)
                        {
                            float costA = Vector2.Distance(det0.wristNorm, lastLeftWrist2D) +
                                          Vector2.Distance(det1.wristNorm, lastRightWrist2D);

                            float costB = Vector2.Distance(det0.wristNorm, lastRightWrist2D) +
                                          Vector2.Distance(det1.wristNorm, lastLeftWrist2D);

                            det0IsLeft = (costA <= costB);
                        }
                        else
                        {
                            // First frame: in mirror mode, hand on screen-left (smaller X) is Left hand
                            det0IsLeft = (det0.wristNorm.x <= det1.wristNorm.x);
                        }
                    }

                    pendingLeftSeen = true;
                    pendingRightSeen = true;

                    // Guarantee: Never write to the same hand twice in a frame
                    if (det0IsLeft)
                    {
                        CopyLandmarks(result, det0.index, pendingLeftNorm, pendingLeftWorld);
                        CopyLandmarks(result, det1.index, pendingRightNorm, pendingRightWorld);
                    }
                    else
                    {
                        CopyLandmarks(result, det1.index, pendingLeftNorm, pendingLeftWorld);
                        CopyLandmarks(result, det0.index, pendingRightNorm, pendingRightWorld);
                    }
                }

                if (image != null && image.Height() > 0)
                {
                    pendingImageAspect = (float)image.Width() / image.Height();
                }

                hasNewData = true;
            }
        }

        private static void CopyLandmarks(
            HandLandmarkerResult result,
            int sourceIndex,
            NormalizedLandmark[] dstNorm,
            Landmark[] dstWorld)
        {
            var normList = result.handLandmarks[sourceIndex].landmarks;
            var worldList = result.handWorldLandmarks[sourceIndex].landmarks;

            for (var j = 0; j < 21; j++)
            {
                dstNorm[j] = normList[j];
                dstWorld[j] = worldList[j];
            }
        }

        private void ProcessHand(
            Handedness handedness,
            bool seen,
            NormalizedLandmark[] normLms,
            Landmark[] worldLms,
            Pose[] rawPoses,
            Pose[] smoothedPoses,
            Pose[] outputPoses,
            Vector3[] pts,
            ref Vector3 prevOutputPos,
            ref float lastJump,
            ref float smoothedDepth,
            ref Vector3 smoothedWristPos,
            ref Vector3 smoothedNormal,
            ref bool wasTracked,
            ref float lastSeenTime,
            float imageAspect)
        {
            float now = Time.time;

            if (seen)
            {
                lastSeenTime = now;

                if (handedness == Handedness.Left)
                {
                    lastLeftWrist2D = new Vector2(normLms[0].x, normLms[0].y);
                }
                else
                {
                    lastRightWrist2D = new Vector2(normLms[0].x, normLms[0].y);
                }

                // 1. Hand size & depth estimation (in-plane orthogonal axes, immune to noisy z foreshortening)
                float currentHandSize = CalculateHandScreenSize(normLms, imageAspect);

                // Check face visualizer anchor
                float targetRefDist = calibrationDistance;
                if (anchorToFaceDepth && faceVisualizer != null && faceVisualizer.IsCalibrated)
                {
                    targetRefDist = faceVisualizer.EstimatedDepth;
                }

                if (!isCalibrated && currentHandSize > 0.01f)
                {
                    calibratedHandSize = currentHandSize * targetRefDist;
                    isCalibrated = true;
                    Debug.Log($"[MediaPipeXRHandAdapter] Hand calibrated: size = {calibratedHandSize:F4} (reference: {targetRefDist:F2}m)");
                }

                float rawDepth = (isCalibrated && currentHandSize > 0.005f)
                    ? (calibratedHandSize / currentHandSize)
                    : targetRefDist;

                // Clamp depth with optional face depth bounding
                float minClamp = minDepth;
                float maxClamp = maxDepth;
                if (anchorToFaceDepth && faceVisualizer != null && faceVisualizer.IsCalibrated)
                {
                    float faceZ = faceVisualizer.EstimatedDepth;
                    minClamp = Mathf.Max(minDepth, faceZ - 0.7f);
                    maxClamp = Mathf.Min(maxDepth, faceZ + 0.35f);
                }
                rawDepth = Mathf.Clamp(rawDepth, minClamp, maxClamp);

                // Smooth depth
                if (wasTracked && depthSmoothing > 0f)
                {
                    smoothedDepth = Mathf.Lerp(smoothedDepth, rawDepth, 1.0f - depthSmoothing);
                }
                else
                {
                    smoothedDepth = rawDepth;
                }

                // 2. Wrist 3D camera position
                var wristLm = normLms[0];
                Vector3 rawWristCamPos = ComputeCameraPosition(wristLm.x, wristLm.y, smoothedDepth, imageAspect);
                if (mirrorX)
                {
                    rawWristCamPos.x = -rawWristCamPos.x;
                }

                if (wasTracked && wristPositionSmoothing > 0f)
                {
                    smoothedWristPos = Vector3.Lerp(smoothedWristPos, rawWristCamPos, 1.0f - wristPositionSmoothing);
                }
                else
                {
                    smoothedWristPos = rawWristCamPos;
                }

                // 3. Precompute landmark 3D positions in camera space from metric bone offsets
                Vector3 wristWorldRef = GetPosition(worldLms[0]);
                for (var i = 0; i < 21; i++)
                {
                    Vector3 localOffset = GetPosition(worldLms[i]) - wristWorldRef;
                    if (mirrorX)
                    {
                        localOffset.x = -localOffset.x;
                    }
                    pts[i] = smoothedWristPos + localOffset * worldScale;
                }

                // 4. Compute anatomical dorsal normal (pointing out the back of the hand)
                Vector3 wristToMiddle = pts[9] - pts[0];
                Vector3 rawDorsalNormal;
                if (handedness == Handedness.Left)
                {
                    Vector3 indexToPinky = pts[17] - pts[5];
                    rawDorsalNormal = Vector3.Cross(indexToPinky, wristToMiddle).normalized;
                }
                else
                {
                    Vector3 pinkyToIndex = pts[5] - pts[17];
                    rawDorsalNormal = Vector3.Cross(pinkyToIndex, wristToMiddle).normalized;
                }

                if (rawDorsalNormal.sqrMagnitude < 0.0001f)
                {
                    rawDorsalNormal = Vector3.forward;
                }

                if (wasTracked && normalSmoothing > 0f)
                {
                    smoothedNormal = Vector3.Slerp(smoothedNormal, rawDorsalNormal, 1.0f - normalSmoothing);
                }
                else
                {
                    smoothedNormal = rawDorsalNormal;
                }

                Vector3 dorsalNormal = smoothedNormal;

                // 5. Build raw bone poses
                for (var i = 0; i < rawPoses.Length; i++)
                {
                    rawPoses[i] = Pose.identity;
                }

                // Wrist: points forward along the central hand axis (wrist -> middle MCP)
                rawPoses[XRHandJointID.Wrist.ToIndex()] = new Pose(
                    pts[0],
                    ComputeBoneRotation(pts[0], pts[9], dorsalNormal));

                // Thumb
                Vector3 thumbSide = handedness == Handedness.Left
                    ? (pts[5] - pts[17]).normalized
                    : (pts[17] - pts[5]).normalized;
                Vector3 thumbUp = (dorsalNormal + thumbSide * 0.5f).normalized;

                var thumbMetacarpalRot = ComputeBoneRotation(pts[1], pts[2], thumbUp);
                var thumbProximalRot = ComputeBoneRotation(pts[2], pts[3], thumbUp);
                var thumbDistalRot = ComputeBoneRotation(pts[3], pts[4], thumbUp);

                SetPose(rawPoses, XRHandJointID.ThumbMetacarpal, new Pose(pts[1], thumbMetacarpalRot));
                SetPose(rawPoses, XRHandJointID.ThumbProximal, new Pose(pts[2], thumbProximalRot));
                SetPose(rawPoses, XRHandJointID.ThumbDistal, new Pose(pts[3], thumbDistalRot));
                SetPose(rawPoses, XRHandJointID.ThumbTip, new Pose(pts[4], thumbDistalRot));

                // Fingers (Index, Middle, Ring, Little)
                SetFinger(rawPoses, pts, XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal,
                    XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip,
                    5, 6, 7, 8, dorsalNormal);

                SetFinger(rawPoses, pts, XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal,
                    XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip,
                    9, 10, 11, 12, dorsalNormal);

                SetFinger(rawPoses, pts, XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal,
                    XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip,
                    13, 14, 15, 16, dorsalNormal);

                SetFinger(rawPoses, pts, XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal,
                    XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip,
                    17, 18, 19, 20, dorsalNormal);

                // Palm synthesized from the 4 MCP joints
                var palmPos = (pts[5] + pts[9] + pts[13] + pts[17]) * 0.25f;
                SetPose(rawPoses, XRHandJointID.Palm, new Pose(palmPos, rawPoses[XRHandJointID.Wrist.ToIndex()].rotation));

                // 6. Temporal smoothing on all 21 joint poses
                if (!wasTracked)
                {
                    for (var i = 0; i < smoothedPoses.Length; i++)
                    {
                        smoothedPoses[i] = rawPoses[i];
                    }
                    wasTracked = true;
                }
                else
                {
                    float posLerp = 1.0f - jointPositionSmoothing;
                    float rotSlerp = 1.0f - jointRotationSmoothing;

                    for (var i = 0; i < smoothedPoses.Length; i++)
                    {
                        smoothedPoses[i] = new Pose(
                            Vector3.Lerp(smoothedPoses[i].position, rawPoses[i].position, posLerp),
                            Quaternion.Slerp(smoothedPoses[i].rotation, rawPoses[i].rotation, rotSlerp));
                    }
                }

                // 7. World-space transformation if requested
                if (posesInWorldSpace && targetCamera != null)
                {
                    for (var i = 0; i < smoothedPoses.Length; i++)
                    {
                        outputPoses[i] = new Pose(
                            cachedCameraPosition + cachedCameraRotation * smoothedPoses[i].position,
                            cachedCameraRotation * smoothedPoses[i].rotation);
                    }
                }
                else
                {
                    Array.Copy(smoothedPoses, outputPoses, smoothedPoses.Length);
                }

                Vector3 currentWrist = outputPoses[0].position;
                if (wasTracked)
                {
                    float dist = Vector3.Distance(currentWrist, prevOutputPos);
                    lastJump = dist;
                    if (enableDebugLogging && dist > 0.04f)
                    {
                        Debug.LogWarning($"[HandAdapter.JUMP] F:{Time.frameCount} | {handedness} jumped {dist * 100f:F1}cm | from {prevOutputPos.ToString("F3")} to {currentWrist.ToString("F3")}");
                    }
                }
                prevOutputPos = currentWrist;

                subsystem?.SetHand(handedness, outputPoses);
                subsystem?.SetHandTracked(handedness, true);
            }
            else
            {
                // Hand was not seen in this specific frame:
                // Check if within the tracking lost timeout grace period
                if (wasTracked && (now - lastSeenTime) < trackingLostTimeout)
                {
                    // Grace period active: keep hand alive at current smoothed pose
                    if (posesInWorldSpace && targetCamera != null)
                    {
                        for (var i = 0; i < smoothedPoses.Length; i++)
                        {
                            outputPoses[i] = new Pose(
                                cachedCameraPosition + cachedCameraRotation * smoothedPoses[i].position,
                                cachedCameraRotation * smoothedPoses[i].rotation);
                        }
                    }
                    subsystem?.SetHand(handedness, outputPoses);
                    subsystem?.SetHandTracked(handedness, true);
                }
                else
                {
                    // Grace period expired: mark untracked
                    subsystem?.SetHandTracked(handedness, false);
                    wasTracked = false;
                    lastJump = 0f;
                }
            }
        }

        private Vector3 ComputeCameraPosition(float u, float v, float z, float aspect)
        {
            float tanHalfFovY = Mathf.Tan(cachedFovY * 0.5f * Mathf.Deg2Rad);
            float tanHalfFovX = tanHalfFovY * aspect;

            float xCam = (u - 0.5f) * 2.0f * z * tanHalfFovX;
            float yCam = (0.5f - v) * 2.0f * z * tanHalfFovY;
            float zCam = z;

            return new Vector3(xCam, yCam, zCam);
        }

        /// <summary>
        /// Computes palm span using orthogonal axes in the image plane.
        /// Rotation-invariant in 2D and immune to noisy NormalizedLandmark.z.
        /// </summary>
        private static float CalculateHandScreenSize(NormalizedLandmark[] normLms, float aspect)
        {
            // Longitudinal axis: Wrist (0) to Middle MCP (9)
            float dxLong = (normLms[9].x - normLms[0].x) * aspect;
            float dyLong = normLms[9].y - normLms[0].y;
            float lenSqLong = dxLong * dxLong + dyLong * dyLong;

            // Transverse axis: Index MCP (5) to Pinky MCP (17)
            float dxTrans = (normLms[17].x - normLms[5].x) * aspect;
            float dyTrans = normLms[17].y - normLms[5].y;
            float lenSqTrans = dxTrans * dxTrans + dyTrans * dyTrans;

            return Mathf.Sqrt(lenSqLong + lenSqTrans);
        }

        private void SetFinger(
            Pose[] poses,
            Vector3[] pts,
            XRHandJointID metacarpal,
            XRHandJointID proximal,
            XRHandJointID intermediate,
            XRHandJointID distal,
            XRHandJointID tip,
            int mcp,
            int pip,
            int dip,
            int fingertip,
            Vector3 dorsalNormal)
        {
            // 1. Metacarpal base: starts near the wrist and extends through the palm to the MCP knuckle
            Vector3 metacarpalPos = Vector3.Lerp(pts[0], pts[mcp], 0.15f);
            Quaternion metacarpalRot = ComputeBoneRotation(metacarpalPos, pts[mcp], dorsalNormal);
            SetPose(poses, metacarpal, new Pose(metacarpalPos, metacarpalRot));

            // 2. Proximal joint (Knuckle / MCP): spans from MCP to PIP
            Vector3 proxPos = pts[mcp];
            Quaternion proxRot = ComputeBoneRotation(proxPos, pts[pip], dorsalNormal);
            SetPose(poses, proximal, new Pose(proxPos, proxRot));

            // 3. Intermediate joint (PIP): spans from PIP to DIP
            Vector3 interPos = pts[pip];
            Quaternion interRot = ComputeBoneRotation(interPos, pts[dip], dorsalNormal);
            SetPose(poses, intermediate, new Pose(interPos, interRot));

            // 4. Distal joint (DIP): spans from DIP to Tip
            Vector3 distPos = pts[dip];
            Quaternion distRot = ComputeBoneRotation(distPos, pts[fingertip], dorsalNormal);
            SetPose(poses, distal, new Pose(distPos, distRot));

            // 5. Tip joint (Fingertip): shares distal rotation
            Vector3 tipPos = pts[fingertip];
            SetPose(poses, tip, new Pose(tipPos, distRot));
        }

        private static Quaternion ComputeBoneRotation(Vector3 start, Vector3 end, Vector3 upReference)
        {
            Vector3 forward = end - start;
            if (forward.sqrMagnitude < 0.000001f)
            {
                return Quaternion.identity;
            }

            forward.Normalize();

            // Project upReference onto the plane orthogonal to forward
            Vector3 up = upReference - forward * Vector3.Dot(upReference, forward);
            if (up.sqrMagnitude < 0.000001f)
            {
                up = Vector3.Cross(forward, Vector3.right);
                if (up.sqrMagnitude < 0.000001f)
                {
                    up = Vector3.Cross(forward, Vector3.up);
                }
            }
            up.Normalize();

            return Quaternion.LookRotation(forward, up);
        }

        private static Vector3 GetPosition(Landmark p)
        {
            return new Vector3(
                p.x,
                -p.y,
                p.z);
        }

        private static void SetPose(
            Pose[] poses,
            XRHandJointID id,
            Pose pose)
        {
            poses[id.ToIndex()] = pose;
        }

        private void OnGUI()
        {
            if (!showOnScreenDebug) return;

            GUI.color = Color.white;
            GUI.Box(new Rect(10, 10, 360, 100), "MediaPipe XR Hand Tracking HUD");

            GUILayout.BeginArea(new Rect(20, 32, 340, 75));
            GUILayout.Label($"Frame: {Time.frameCount} | Calibrated: {isCalibrated} (Size: {calibratedHandSize:F3})");

            string leftStatus = leftWasTracked ? $"Tracked (Jump: {lastLeftJump * 100f:F1}cm)" : "Lost";
            string leftPos = leftWasTracked ? leftOutputPoses[0].position.ToString("F3") : "---";
            GUILayout.Label($"Left: {leftStatus} | {leftPos}");

            string rightStatus = rightWasTracked ? $"Tracked (Jump: {lastRightJump * 100f:F1}cm)" : "Lost";
            string rightPos = rightWasTracked ? rightOutputPoses[0].position.ToString("F3") : "---";
            GUILayout.Label($"Right: {rightStatus} | {rightPos}");
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (subsystem != null)
            {
                subsystem.Stop();
                subsystem.Destroy();
                subsystem = null;
            }
        }
    }
}
