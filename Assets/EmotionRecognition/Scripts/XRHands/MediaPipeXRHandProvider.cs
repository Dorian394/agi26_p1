using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.ProviderImplementation;

namespace MediaPipeXRHands
{
    public sealed class MediaPipeXRHandProvider : XRHandSubsystemProvider
    {
        private readonly object sync = new object();

        private readonly Pose[] leftPoses =
            new Pose[XRHandJointID.EndMarker.ToIndex()];

        private readonly Pose[] rightPoses =
            new Pose[XRHandJointID.EndMarker.ToIndex()];

        private bool leftTracked;
        private bool rightTracked;

        public override void GetHandLayout(
            NativeArray<bool> handJointsInLayout)
        {
            for (
                var i = XRHandJointID.BeginMarker.ToIndex();
                i < XRHandJointID.EndMarker.ToIndex();
                i++)
            {
                handJointsInLayout[i] = true;
            }
        }

        public override void Start()
        {
            lock (sync)
            {
                leftTracked = false;
                rightTracked = false;
            }
        }

        public override void Stop()
        {
            lock (sync)
            {
                leftTracked = false;
                rightTracked = false;
            }
        }

        public override void Destroy()
        {
            lock (sync)
            {
                leftTracked = false;
                rightTracked = false;
            }
        }

        public static bool EnableDebugLogging = false;

        public override XRHandSubsystem.UpdateSuccessFlags TryUpdateHands(
            XRHandSubsystem.UpdateType updateType,
            ref Pose leftHandRootPose,
            NativeArray<XRHandJoint> leftHandJoints,
            ref Pose rightHandRootPose,
            NativeArray<XRHandJoint> rightHandJoints)
        {
            XRHandSubsystem.UpdateSuccessFlags flags =
                XRHandSubsystem.UpdateSuccessFlags.None;

            lock (sync)
            {
                if (leftTracked)
                {
                    WriteHand(
                        Handedness.Left,
                        leftPoses,
                        leftHandJoints,
                        ref leftHandRootPose);

                    flags |=
                        XRHandSubsystem.UpdateSuccessFlags.LeftHandRootPose |
                        XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints;
                }

                if (rightTracked)
                {
                    WriteHand(
                        Handedness.Right,
                        rightPoses,
                        rightHandJoints,
                        ref rightHandRootPose);

                    flags |=
                        XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose |
                        XRHandSubsystem.UpdateSuccessFlags.RightHandJoints;
                }

                if (EnableDebugLogging && (leftTracked || rightTracked))
                {
                    string leftStr = leftTracked ? leftHandRootPose.position.ToString("F4") : "untracked";
                    string rightStr = rightTracked ? rightHandRootPose.position.ToString("F4") : "untracked";
                    Debug.Log($"[HandProvider.TryUpdateHands] F:{Time.frameCount} | Type:{updateType} | LeftRoot:{leftStr} | RightRoot:{rightStr}");
                }
            }

            return flags;
        }

        public void SetHand(
            Handedness handedness,
            Pose[] poses)
        {
            lock (sync)
            {
                var destination =
                    handedness == Handedness.Left
                        ? leftPoses
                        : rightPoses;

                for (var i = 0; i < destination.Length; i++)
                {
                    destination[i] = poses[i];
                }

                if (handedness == Handedness.Left)
                    leftTracked = true;
                else
                    rightTracked = true;

                if (EnableDebugLogging)
                {
                    Debug.Log($"[HandProvider.SetHand] F:{Time.frameCount} | {handedness} | WristPos:{poses[0].position.ToString("F4")}");
                }
            }
        }

        public void SetHandTracked(
            Handedness handedness,
            bool tracked)
        {
            lock (sync)
            {
                bool prevTracked = (handedness == Handedness.Left) ? leftTracked : rightTracked;

                if (handedness == Handedness.Left)
                    leftTracked = tracked;
                else
                    rightTracked = tracked;

                if (EnableDebugLogging && prevTracked != tracked)
                {
                    Debug.Log($"[HandProvider.SetHandTracked] F:{Time.frameCount} | {handedness} -> {tracked}");
                }
            }
        }

        private static void WriteHand(
            Handedness handedness,
            Pose[] poses,
            NativeArray<XRHandJoint> joints,
            ref Pose rootPose)
        {
            for (
                var i = XRHandJointID.BeginMarker.ToIndex();
                i < XRHandJointID.EndMarker.ToIndex();
                i++)
            {
                var jointId =
                    XRHandJointIDUtility.FromIndex(i);

                joints[i] =
                    XRHandProviderUtility.CreateJoint(
                        handedness,
                        XRHandJointTrackingState.Pose,
                        jointId,
                        poses[i]);
            }

            rootPose =
                poses[XRHandJointID.Wrist.ToIndex()];
        }
    }
}