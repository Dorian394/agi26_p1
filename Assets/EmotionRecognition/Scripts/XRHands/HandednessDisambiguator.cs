using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;

namespace MediaPipeXRHands
{
    public class HandednessDisambiguator
    {
        private Vector2 lastLeftWrist2D = new Vector2(0.3f, 0.5f);
        private Vector2 lastRightWrist2D = new Vector2(0.7f, 0.5f);
        private int singleHandFlipStreak = 0;
        private bool leftWasTracked = false;
        private bool rightWasTracked = false;

        private struct RawDetection
        {
            public string category;
            public Vector2 wristNorm;
            public int index;
        }

        public void Reset()
        {
            lastLeftWrist2D = new Vector2(0.3f, 0.5f);
            lastRightWrist2D = new Vector2(0.7f, 0.5f);
            singleHandFlipStreak = 0;
            leftWasTracked = false;
            rightWasTracked = false;
        }

        public (int leftIndex, int rightIndex) Disambiguate(HandLandmarkerResult result)
            => Disambiguate(result.handedness, result.handLandmarks);

        public (int leftIndex, int rightIndex) Disambiguate(GestureRecognizerResult result)
            => Disambiguate(result.handedness, result.handLandmarks);

        public (int leftIndex, int rightIndex) Disambiguate(
            List<Classifications> handedness,
            List<NormalizedLandmarks> handLandmarks)
        {
            int leftIndex = -1;
            int rightIndex = -1;

            // 1. Gather all valid hand detections (up to 2)
            RawDetection det0 = default;
            RawDetection det1 = default;
            int validCount = 0;

            if (handedness != null && handLandmarks != null)
            {
                int max = Mathf.Min(handedness.Count, handLandmarks.Count);
                for (var i = 0; i < max && validCount < 2; i++)
                {
                    var categories = handedness[i].categories;
                    var landmarks = handLandmarks[i].landmarks;
                    if (categories == null || categories.Count == 0 || landmarks == null || landmarks.Count < 21)
                        continue;

                    var det = new RawDetection
                    {
                        category = categories[0].categoryName,
                        wristNorm = new Vector2(landmarks[0].x, landmarks[0].y),
                        index = i
                    };

                    if (validCount++ == 0) det0 = det;
                    else det1 = det;
                }
            }

            // 2. Disambiguate and assign detections to Left and Right
            if (validCount == 1)
            {
                int predictedHand = (det0.category == "Left") ? 0 : 1;
                int assignedHand = predictedHand;

                bool onlyLeft = leftWasTracked && !rightWasTracked;
                bool onlyRight = rightWasTracked && !leftWasTracked;
                if (onlyLeft || onlyRight)
                {
                    int trackedHand = onlyLeft ? 0 : 1;
                    Vector2 lastWrist = onlyLeft ? lastLeftWrist2D : lastRightWrist2D;

                    if (predictedHand != trackedHand && Vector2.Distance(det0.wristNorm, lastWrist) < 0.3f && singleHandFlipStreak < 2)
                    {
                        singleHandFlipStreak++;
                        assignedHand = trackedHand;
                    }
                    else
                    {
                        singleHandFlipStreak = (predictedHand != trackedHand) ? singleHandFlipStreak + 1 : 0;
                        assignedHand = predictedHand;
                    }
                }
                else
                {
                    singleHandFlipStreak = 0;
                }

                if (assignedHand == 0)
                {
                    leftIndex = det0.index;
                    lastLeftWrist2D = det0.wristNorm;
                }
                else
                {
                    rightIndex = det0.index;
                    lastRightWrist2D = det0.wristNorm;
                }
            }
            else if (validCount == 2)
            {
                singleHandFlipStreak = 0;
                bool det0IsLeft = (det0.category != det1.category)
                    ? (det0.category == "Left")
                    : (leftWasTracked || rightWasTracked)
                        ? (Vector2.Distance(det0.wristNorm, lastLeftWrist2D) + Vector2.Distance(det1.wristNorm, lastRightWrist2D)
                           <= Vector2.Distance(det0.wristNorm, lastRightWrist2D) + Vector2.Distance(det1.wristNorm, lastLeftWrist2D))
                        : (det0.wristNorm.x <= det1.wristNorm.x);

                var (leftDet, rightDet) = det0IsLeft ? (det0, det1) : (det1, det0);
                leftIndex = leftDet.index;
                lastLeftWrist2D = leftDet.wristNorm;
                rightIndex = rightDet.index;
                lastRightWrist2D = rightDet.wristNorm;
            }

            leftWasTracked = (leftIndex >= 0);
            rightWasTracked = (rightIndex >= 0);

            return (leftIndex, rightIndex);
        }
    }
}
