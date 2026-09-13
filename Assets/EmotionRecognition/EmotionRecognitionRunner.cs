/* This code was produced by following the Homuler tutorial:
 * https://github.com/homuler/MediaPipeUnityPlugin/blob/master/docs/Tutorial-Task-API.md 
 */


using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Experimental;
using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;


namespace Assets.EmotionRecognition
{
    public class EmotionRecognitionRunner : MonoBehaviour
    {
        [SerializeField] private RawImage screen;
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private int fps;
        [SerializeField] TextAsset modelAsset;

        WebCamTexture webCamTexture;
        FaceLandmarker faceLandmarker;
        TextureFrame textureFrame;

        IEnumerator Start()
        {

            // Init
            yield return CreateWebCamTexture();
            CreateFaceLandmarkerTask();

            // Loop
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var waitForEndOfFrame = new WaitForEndOfFrame();
            textureFrame = new TextureFrame(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32);

            while (true)
            {
                ProcessFrame(stopwatch);

                yield return waitForEndOfFrame;
            }
        }


        IEnumerator CreateWebCamTexture()
        {
            if (WebCamTexture.devices.Length == 0)
            {
                throw new System.Exception("Web Camera devices are not found");
            }
            var webCamDevice = WebCamTexture.devices[0];
            webCamTexture = new WebCamTexture(webCamDevice.name, width, height, fps);
            webCamTexture.Play();

            // NOTE: On macOS, the contents of webCamTexture may not be readable immediately, so wait until it is readable
            yield return new WaitUntil(() => webCamTexture.width > 16);

            screen.rectTransform.sizeDelta = new Vector2(width, height);
            screen.texture = webCamTexture;
            screen.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // Flip screen horizontally for mirror effect
        }

        void CreateFaceLandmarkerTask()
        {
            var options = new FaceLandmarkerOptions(
                baseOptions: new Mediapipe.Tasks.Core.BaseOptions(
                    Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU,
                    modelAssetBuffer: modelAsset.bytes
                ),
                runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM,
                outputFaceBlendshapes: true,
                resultCallback: OnFaceLandmarkerResult
            );

           faceLandmarker = FaceLandmarker.CreateFromOptions(options);
        }

        void ProcessFrame(Stopwatch stopwatch)
        {
            textureFrame.ReadTextureOnCPU(webCamTexture, flipHorizontally: false, flipVertically: true);
            using var image = textureFrame.BuildCPUImage();
            faceLandmarker.DetectAsync(image, stopwatch.ElapsedMilliseconds);
        }

        // Emotion classification
        private void OnFaceLandmarkerResult(FaceLandmarkerResult result, Mediapipe.Image image, long timestamp)
        {
            if (result.faceLandmarks != null && result.faceBlendshapes.Count > 0)
            {
                var blendshapes = result.faceBlendshapes[0].categories;

                // Get each blendshape value
                float smileLeft = GetBlendshapeValue(blendshapes, "mouthSmileLeft");
                float smileRight = GetBlendshapeValue(blendshapes, "mouthSmileRight");
                float jawOpen = GetBlendshapeValue(blendshapes, "jawOpen");
                float browDownLeft = GetBlendshapeValue(blendshapes, "browDownLeft");
                float browDownRight = GetBlendshapeValue(blendshapes, "browDownRight");
                float browInnerUp = GetBlendshapeValue(blendshapes, "browInnerUp");
                float browOuterUpLeft = GetBlendshapeValue(blendshapes, "browOuterUpLeft");
                float browOuterUpRight = GetBlendshapeValue(blendshapes, "browOuterUpRight");

                // Average
                float browDown = (browDownLeft + browDownRight) / 2.0f;
                float smile = (smileLeft + smileRight) / 2.0f;
                float browUp = (browInnerUp + browOuterUpLeft + browOuterUpRight) / 3.0f;

                // Recognize emotion
                if (smile > 0.6f)
                {
                    UnityEngine.Debug.Log("Happy :)");
                }
                else if (jawOpen > 0.3f || browUp > 0.5f)
                {
                    UnityEngine.Debug.Log("Surprised :o");
                }
                else if (browDown > 0.3f)
                {
                    UnityEngine.Debug.Log("Angry >:(");
                }
                else
                {
                    UnityEngine.Debug.Log("Neutral :|");
                }
            }
        }

        private float GetBlendshapeValue(System.Collections.Generic.List<Mediapipe.Tasks.Components.Containers.Category> categories, string name)
        {
            foreach (var category in categories)
            {
                if (category.categoryName == name) return category.score;
            }
            return 0f;
        }


        private void OnDestroy()
        {
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
            }
            textureFrame?.Release();
            faceLandmarker?.Close();
        }

    }
}
