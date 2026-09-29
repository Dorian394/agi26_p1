using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.ProviderImplementation;

namespace MediaPipeXRHands
{
    public sealed class MediaPipeXRHandSubsystem
        : XRHandSubsystem
    {
        public const string Id =
            "mediapipe-holistic-xr-hands";

        private MediaPipeXRHandProvider HandProvider =>
            provider as MediaPipeXRHandProvider;

        private XRHandProviderUtility.SubsystemUpdater updater;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDescriptor()
        {
            XRHandSubsystemDescriptor.Register(
                new XRHandSubsystemDescriptor.Cinfo
                {
                    id = Id,
                    providerType =
                        typeof(MediaPipeXRHandProvider),
                    subsystemTypeOverride =
                        typeof(MediaPipeXRHandSubsystem),

                    supportsAimPose = false,
                    supportsAimActivateValue = false,
                    supportsGraspValue = false,
                    supportsGripPose = false,
                    supportsPinchPose = false,
                    supportsPinchValue = false,
                    supportsPokePose = false
                });
        }

        public static MediaPipeXRHandSubsystem Create()
        {
            var descriptors =
                new System.Collections.Generic.List<
                    XRHandSubsystemDescriptor>();

            SubsystemManager.GetSubsystemDescriptors(
                descriptors);

            foreach (var descriptor in descriptors)
            {
                if (descriptor.id != Id)
                    continue;

                return descriptor.Create()
                    as MediaPipeXRHandSubsystem;
            }

            return null;
        }

        public void SetHand(
            Handedness handedness,
            Pose[] poses)
        {
            HandProvider?.SetHand(handedness, poses);
        }

        public void SetHandTracked(
            Handedness handedness,
            bool tracked)
        {
            HandProvider?.SetHandTracked(
                handedness,
                tracked);
        }

        protected override void OnStart()
        {
            base.OnStart();

            if (updater == null)
            {
                updater =
                    new XRHandProviderUtility.SubsystemUpdater(
                        this);
            }

            updater.Start();
        }

        protected override void OnStop()
        {
            updater?.Stop();

            base.OnStop();
        }

        protected override void OnDestroy()
        {
            updater?.Destroy();
            updater = null;

            base.OnDestroy();
        }
    }
}
