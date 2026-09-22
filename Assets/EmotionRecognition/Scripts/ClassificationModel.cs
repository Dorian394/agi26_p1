using Mediapipe;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity.Experimental;
using Micrograd;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

public class ClassificationModel
{
    // Micrograd
    private List<Value[]> inputData;
    private List<Value[]> outputData;
    MLP nn;
    [SerializeField] private bool trainModel;

    // Mediapipe
    const int nbBlendshapes = 52;
    const int nbEmotions = 5;
    private TextAsset modelAsset;
    string folderPath = "Data";
    FaceLandmarker faceLandmarker;

    public ClassificationModel(bool trainModel, TextAsset modelAsset)
    {
        this.trainModel = trainModel;
        this.modelAsset = modelAsset;

        InitNetworkArchitecture();

        if (trainModel)
        {
            GetLandmarkLists();
            Train();
            SaveModel();
        }

        LoadModel();
    }

    void Update()
    {

    }

    private void GetLandmarkLists()
    {
        // Create the list
        inputData = new List<Value[]>();
        outputData = new List<Value[]>();

        // Create the mediapipe task
        CreateFaceLandmarkerTask();

        // Get the path to the database
        string trainPath = Path.Combine(Application.streamingAssetsPath, folderPath);

        trainPath = Path.Combine(trainPath, "train");

        if (Directory.Exists(trainPath))
        {
            for (int emotion = 0; emotion < nbEmotions; emotion++)
            {

                string fullPath = Path.Combine(trainPath, GetEmotionFromIndex(emotion));
                if (Directory.Exists(fullPath))
                {

                    foreach (string filePath in Directory.EnumerateFiles(fullPath))
                    {
                        // Skip the .meta files
                        if (filePath.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Create an image to pass to the faceLandmarker
                        Image image = LoadImageFromFile(filePath);
                        if (image == null)
                        {
                            UnityEngine.Debug.LogError($"Error while importing the file: {filePath}");
                            return;
                        }

                        // Get the results
                        var result = faceLandmarker.Detect(image);

                        // Save the results
                        if (result.faceBlendshapes.Count > 0)
                        {
                            var blendshapes = result.faceBlendshapes[0].categories;
                            if (blendshapes.Count >= nbBlendshapes)
                            {
                                // Save input
                                inputData.Add(ConvertBlendshapesToValue(blendshapes));

                                // Save output
                                float[] output = new float[nbEmotions];
                                output[emotion] = 1.0f;
                                outputData.Add(Value.Convert(output));
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogError($"The folder does not exist: {fullPath}");
                }
            }
        }
        else
        {
            Debug.LogError($"The folder does not exist: {trainPath}");
        }
    }


    private string GetEmotionFromIndex(int index)
    {
        switch (index)
        {
            case 0: return "neutral";
            case 1: return "happy";
            case 2: return "sad";
            case 3: return "angry";
            case 4: return "surprised";
            default: return "unknown";
        }
    }

    private Value[] ConvertBlendshapesToValue(List<Category> blendshapes)
    {
        float[] blendshape_values = new float[nbBlendshapes];
        for (int i = 0; i < nbBlendshapes; i++)
        {
            blendshape_values[i] = blendshapes[i].score;
        }
        return Value.Convert(blendshape_values);
    }

    public Image LoadImageFromFile(string filePath)
    {
        // Create a Texture2D
        if (!File.Exists(filePath)) return null;

        byte[] fileData = File.ReadAllBytes(filePath);

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, fileData)) return null;

        // Create a TextureFrame
        int width = texture.width;
        int height = texture.height;

        TextureFrame textureFrame = new TextureFrame(width, height, TextureFormat.RGBA32);
        textureFrame.ReadTextureOnCPU(texture, flipHorizontally: true, flipVertically: true);

        // Create an Image
        Image image = textureFrame.BuildCPUImage();
        textureFrame?.Release();
        return image;
    }

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

    // Micrograd
    void Train()
    {
        MicroMath.Random.Seed(0);

        InitNetworkArchitecture();

        // Optimizer that will do gradient descent for us
        Adam optimizer = nn.Adam_Optimizer(nn.GetParameters(), learningRate: 0.01f);

        // Create input data list
        int halfSize = inputData.Count / 2;
        Value[][] input = inputData.ToArray();
        Value[][] output = outputData.ToArray();

        // Train
        int epochs = 10;

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            float totalEpochLoss = 0f;
            var indices = Enumerable.Range(0, input.Length).OrderBy(x => UnityEngine.Random.value).ToArray();

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
            System.GC.Collect();
        }
    }

    public int getEmotion(List<Category> blendshapes)
    {
        Value[] input = ConvertBlendshapesToValue(blendshapes);
        Value[] result = nn.Activate(input);
        return IdxMax(result);
    }

    int IdxMax(Value[] values)
    {
        int maxIndex = 0;
        float maxValue = values[0].data;

        for (int i = 1; i < values.Length; i++)
        {
            if (values[i].data > maxValue)
            {
                maxValue = values[i].data;
                maxIndex = i;
            }
        }
        return maxIndex;
    }

    public void SaveModel(string fileName = "emotion_model.json")
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

        string filePath = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(filePath, json);

        Debug.Log($"The model was successful saved.");
    }


    public void LoadModel(string fileName = "emotion_model.json")
    {
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

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

    private void InitNetworkArchitecture()
    {
        nn = new MLP();
        nn.AddLayers(
            nn.Linear(nbBlendshapes, 16),
            nn.ReLU(),
            nn.Linear(16, nbEmotions),
            nn.Softmax()
        );
    }
}
