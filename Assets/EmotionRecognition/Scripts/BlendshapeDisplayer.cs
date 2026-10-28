using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mediapipe.Tasks.Components.Containers;
using Google.Protobuf.WellKnownTypes;

public class BlendshapeDisplayer : MonoBehaviour
{
    [SerializeField] private GameObject barPrefab;
    [SerializeField] private Transform containerTransform;

    private readonly string[] allBlendshapeNames = new string[]
    {
        "_neutral",
        "browDownLeft", "browDownRight", "browInnerUp", "browOuterUpLeft", "browOuterUpRight",
        "cheekPuff", "cheekSquintLeft", "cheekSquintRight",
        "eyeBlinkLeft", "eyeBlinkRight", "eyeLookDownLeft", "eyeLookDownRight",
        "eyeLookInLeft", "eyeLookInRight", "eyeLookOutLeft", "eyeLookOutRight",
        "eyeLookUpLeft", "eyeLookUpRight", "eyeSquintLeft", "eyeSquintRight",
        "eyeWideLeft", "eyeWideRight",
        "jawForward", "jawLeft", "jawOpen", "jawRight",
        "mouthClose", "mouthDimpleLeft", "mouthDimpleRight",
        "mouthFrownLeft", "mouthFrownRight", "mouthFunnel",
        "mouthPressLeft", "mouthPressRight",
        "mouthPucker", "mouthRollLower", "mouthRollUpper",
        "mouthShrugLower", "mouthShrugUpper",
        "mouthSmileLeft", "mouthSmileRight",
        "mouthStretchLeft", "mouthStretchRight",
        "mouthLowerDownLeft", "mouthLowerDownRight",
        "mouthUpperUpLeft", "mouthUpperUpRight",
        "tongueOut"
    };

    private Dictionary<string, float> blendshapeValues = new Dictionary<string, float>();
    private Dictionary<string, Image> barImages = new Dictionary<string, Image>();

    void Start()
    {
        foreach (var name in allBlendshapeNames)
        {
            if (!blendshapeValues.ContainsKey(name))
            {
                blendshapeValues.Add(name, 0f);
            }

            if (barPrefab != null && containerTransform != null)
            {
                GameObject barInstance = Instantiate(barPrefab, containerTransform);

                TMP_Text nameText = barInstance.GetComponentInChildren<TMP_Text>();
                if (nameText != null)
                {
                    nameText.text = name;
                }

                Image fillImage = barInstance.transform.Find("Fill").GetComponent<Image>();
                if (fillImage != null)
                {
                    if (!barImages.ContainsKey(name))
                    {
                        barImages.Add(name, fillImage);
                    }
                }
            }
        }
    }

    public void UpdateAllBlendshapes(List<Category> blendshapes)
    {
        foreach (var name in allBlendshapeNames)
        {
            float value = GetBlendshapeValue(blendshapes, name);
            blendshapeValues[name] = value;
        }
    }

    void Update()
    {
        foreach (var name in allBlendshapeNames)
        {
            if (barImages.TryGetValue(name, out Image img))
            {
                if(blendshapeValues.TryGetValue(name, out float value))
                {
                    img.fillAmount = value;
                }
            }
        }
    }

    public float GetValue(string blendshapeName)
    {
        if (blendshapeValues.TryGetValue(blendshapeName, out float value))
        {
            return value;
        }
        return 0f;
    }

    private float GetBlendshapeValue(List<Category> categories, string name)
    {
        foreach (var category in categories)
        {
            if (category.categoryName == name) return category.score;
        }
        return 0f;
    }
}
