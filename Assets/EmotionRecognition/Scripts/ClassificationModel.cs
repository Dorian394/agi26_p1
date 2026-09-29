using Mediapipe;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Experimental;
using Micrograd;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class ClassificationModel
{
    // Constant parameters
    const int nbBlendshapes = 52;
    const int nbEmotions = 4;
    const string folderPath = "Data";
    const int width = 256, height = 256;
    const int maxImgPerEmotion = 3000;

    // Micrograd
    private List<Value[]> inputData;
    private List<Value[]> outputData;
    MLP nn;
    [SerializeField] private bool trainModel;

    // Mediapipe
    private TextAsset modelAsset;
    FaceLandmarker faceLandmarker;


    // Reusable objects
    private TextureFrame textureFrame = null;
    private Texture2D sourceTexture;   
    private Texture2D resizedTexture; 

    public ClassificationModel(bool trainModel, TextAsset modelAsset)
    {
        this.trainModel = trainModel;
        this.modelAsset = modelAsset;

        InitNetworkArchitecture();

        if (trainModel)
        {
            // Init the reusable objects
            textureFrame = new TextureFrame(width, height, TextureFormat.RGBA32);
            sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            resizedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                GetLandmarkLists();
            }
            finally
            {
                // Always release temporary resources, even if extraction throws
                ReleaseExtractionResources();
            }

            if (Train())
            {
                SaveModel();
            }
        }

        LoadModel();
    }

    private void ReleaseExtractionResources()
    {
        if (faceLandmarker != null)
        {
            faceLandmarker.Close();
            faceLandmarker = null;
        }
        if (sourceTexture != null)
        {
            UnityEngine.Object.Destroy(sourceTexture);
            sourceTexture = null;
        }
        if (resizedTexture != null)
        {
            UnityEngine.Object.Destroy(resizedTexture);
            resizedTexture = null;
        }
    }

    private void GetLandmarkLists()
    {
        // Init output/input list
        inputData = new List<Value[]>();
        outputData = new List<Value[]>();

        // Init Mediapipe Task
        CreateFaceLandmarkerTask();

        // Find the folder with the dataset
        string trainPath = Path.Combine(Application.streamingAssetsPath, folderPath);
        trainPath = Path.Combine(trainPath, "train");

        if (!Directory.Exists(trainPath))
        {
            Debug.LogError($"The folder does not exist: {trainPath}");
            return;
        }

        // Update list of inputs/outputs for each emotion
        for (int emotion = 0; emotion < nbEmotions; emotion++)
        {
            // Check if the folder exists
            string fullPath = Path.Combine(trainPath, GetEmotionFromIndex(emotion));
            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"The folder does not exist: {fullPath}");
                continue;
            }

            // Get all files within the folder
            var filePaths = Directory.EnumerateFiles(fullPath)
                .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            int length = Math.Min(filePaths.Length, maxImgPerEmotion);

            // Read all files in parallel
            byte[][] fileBytes = new byte[length][];
            Parallel.For(0, length, i =>
            {
                try
                {
                    fileBytes[i] = File.ReadAllBytes(filePaths[i]);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error reading file {filePaths[i]}: {e.Message}");
                    fileBytes[i] = null;
                }
            });

            // Pass the images in the Mediapipe pipeline to extract Blendshapes
            int kept = 0;
            for (int i = 0; i < fileBytes.Length; i++)
            {
                Image image = LoadImageFromBytes(fileBytes[i]);
                if (image == null)
                {
                    Debug.LogError($"Error while loading the image {filePaths[i]} for emotion {emotion}");
                    continue;
                }

                try
                {
                    var result = faceLandmarker.Detect(image);

                    if (result.faceBlendshapes != null && result.faceBlendshapes.Count > 0)
                    {
                        var blendshapes = result.faceBlendshapes[0].categories;
                        if (blendshapes != null && blendshapes.Count >= nbBlendshapes)
                        {
                            inputData.Add(ConvertBlendshapesToValue(blendshapes));

                            float[] output = new float[nbEmotions];
                            for(int k = 0; k < nbEmotions; k++)
                            {
                                output[k] = 0.1f / (nbEmotions - 1);
                            }
                            output[emotion] = 0.9f;
                            outputData.Add(Value.Convert(output));
                            kept++;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Detect() failed for {filePaths[i]}: {e.Message}");
                }
                finally
                {
                    image.Dispose();
                }

                fileBytes[i] = null; // let the GC reclaim the raw bytes early
            }
            // Print debug
            Debug.Log($"{GetEmotionFromIndex(emotion)}: {kept}/{filePaths.Length} images with a detected face");
        }
    }

    // Get the index corresponding to each emotion
    private string GetEmotionFromIndex(int index)
    {
        switch (index)
        {
            case 0: return "neutral";
            case 1: return "happy";
            case 2: return "angry";
            case 3: return "surprised";
            default: return "unknown";
        }
    }

    // Convert the Blendshapes coefficients (list of float) to Micrograd Values
    private Value[] ConvertBlendshapesToValue(List<Category> blendshapes)
    {
        float[] blendshape_values = new float[nbBlendshapes];

        var sortedBlendshapes = blendshapes.OrderBy(b => b.index).ToList();

        for (int i = 0; i < nbBlendshapes && i < sortedBlendshapes.Count; i++)
        {
            blendshape_values[i] = sortedBlendshapes[i].score;
        }
        return Value.Convert(blendshape_values);
    }

    // Read a list of bytes file and returns a corresponding Image that can be used by Mediapipe
    private Image LoadImageFromBytes(byte[] fileData)
    {
        if (fileData == null || fileData.Length == 0)
        {
            return null;
        }

        if (!ImageConversion.LoadImage(sourceTexture, fileData))
        {
            return null;
        }

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Graphics.Blit(sourceTexture, rt);
            RenderTexture.active = rt;
            resizedTexture.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
            resizedTexture.Apply();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }

        textureFrame.ReadTextureOnCPU(resizedTexture, flipHorizontally: true, flipVertically: true);

        return textureFrame.BuildCPUImage();
    }

    // Initialize the Mediapipe Face Landmark task
    void CreateFaceLandmarkerTask()
    {
        var options = new FaceLandmarkerOptions(
            baseOptions: new Mediapipe.Tasks.Core.BaseOptions(
                Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU,
                modelAssetBuffer: modelAsset.bytes
            ),
            runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.IMAGE,
            outputFaceBlendshapes: true
        );

        faceLandmarker = FaceLandmarker.CreateFromOptions(options);
    }


    // Train the model
    bool Train()
    {
        if (inputData == null || inputData.Count == 0)
        {
            Debug.LogError("No training samples (no face detected in any image). Training aborted.");
            return false;
        }

        MicroMath.Random.Seed(0);

        InitNetworkArchitecture();

        Adam optimizer = nn.Adam_Optimizer(nn.GetParameters(), learningRate: 0.0001f);

        Value[][] input = inputData.ToArray();
        Value[][] output = outputData.ToArray();

        Debug.Log($"Input size: {input.Length}");

        int epochs = 50;
        int[] indices = Enumerable.Range(0, input.Length).ToArray();

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            float totalEpochLoss = 0f;

            // Fisher-Yates shuffle (O(n))
            for (int s = indices.Length - 1; s > 0; s--)
            {
                int r = UnityEngine.Random.Range(0, s + 1);
                (indices[s], indices[r]) = (indices[r], indices[s]);
            }

            for (int i = 0; i < indices.Length; i++)
            {
                int j = indices[i];
                optimizer.ZeroGrad();

                Value[] result = nn.Activate(input[j]);
                Value sampleLoss = new(0f);

                for (int k = 0; k < nbEmotions; k++)
                {
                    if (output[j][k].data > 0.5f)
                    {
                        sampleLoss -= result[k].Log();
                    }
                }

                totalEpochLoss += sampleLoss.data;

                sampleLoss.Backward();
                optimizer.Step();
            }

            Debug.Log($"Epoch {epoch}: loss = {totalEpochLoss / input.Length}");
        }

        return true;
    }

    // Public interface: returns the list of probabilities
    public float[] getEmotions(List<Category> blendshapes)
    {
        Value[] input = ConvertBlendshapesToValue(blendshapes);
        Value[] result = nn.Activate(input);
        float[] output = new float[result.Length];
        for (int i = 0; i < result.Length; i++)
        {
            output[i] = result[i].data;
        }

        return output;
    }

    // Save the trained model to a JSON file
    void SaveModel(string fileName = "emotion_model.json")
    {
        if (nn == null)
        {
            Debug.LogError("Cannot save the model.");
            return;
        }

        ModelData data = new ModelData();

        Value[] parameters = nn.GetParameters();

        foreach (Value p in parameters)
        {
            data.weightsAndBiases.Add(p.data);
        }

        string json = JsonUtility.ToJson(data, true);

        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);
        File.WriteAllText(filePath, json);

        Debug.Log("The model was successfully saved.");
    }

    // Load the model from a JSON file
    void LoadModel(string fileName = "emotion_model.json")
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(filePath))
        {
            Debug.LogError($"Cannot load the model. Invalid file path: {filePath}");
            return;
        }

        string json = File.ReadAllText(filePath);
        ModelData data = JsonUtility.FromJson<ModelData>(json);

        if (nn == null || nn.GetParameters().Length == 0)
        {
            InitNetworkArchitecture();
        }

        Value[] parameters = nn.GetParameters();

        if (parameters.Length != data.weightsAndBiases.Count)
        {
            Debug.LogError($"Incompatible architecture! The network needs {parameters.Length} parameters, but the file has {data.weightsAndBiases.Count} parameters.");
            return;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            parameters[i].data = data.weightsAndBiases[i];
        }

        Debug.Log($"The model was successfully loaded. ({parameters.Length} parameters).");
    }

    // Initialize the Neural Network Architecture
    private void InitNetworkArchitecture()
    {
        nn = new MLP();
        nn.AddLayers(
            nn.Linear(nbBlendshapes, 2 * nbBlendshapes),
            nn.Tanh(),
            nn.Linear(2 * nbBlendshapes, nbBlendshapes / 2),
            nn.Tanh(),
            nn.Linear(nbBlendshapes / 2, nbEmotions),
            nn.Softmax()
        );
    }
}
