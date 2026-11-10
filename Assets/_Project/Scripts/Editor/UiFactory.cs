using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Tao phan tu uGUI/TMP bang code giong menu GameObject > UI cua Unity
internal static class UiFactory
{
    internal static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920f, 1080f);
    internal static readonly Vector2 CENTER = new Vector2(0.5f, 0.5f);
    internal static readonly Vector2 TOP_CENTER = new Vector2(0.5f, 1f);
    internal static readonly Vector2 BOTTOM_CENTER = new Vector2(0.5f, 0f);

    private const string UI_LAYER = "UI";
    private const string SPRITE_STANDARD = "UI/Skin/UISprite.psd";
    private const string SPRITE_BACKGROUND = "UI/Skin/Background.psd";
    private const string SPRITE_INPUT_FIELD = "UI/Skin/InputFieldBackground.psd";
    private const string SPRITE_KNOB = "UI/Skin/Knob.psd";
    private const string SPRITE_CHECKMARK = "UI/Skin/Checkmark.psd";
    private const string SPRITE_DROPDOWN = "UI/Skin/DropdownArrow.psd";
    private const string SPRITE_MASK = "UI/Skin/UIMask.psd";

    internal static Sprite StandardSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>(SPRITE_STANDARD);

    internal static RectTransform Place(GameObject element, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        element.layer = LayerMask.NameToLayer(UI_LAYER);
        RectTransform rect = element.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = element.AddComponent<RectTransform>();
        }

        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    internal static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = CENTER;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    internal static Image CreateImage(Transform parent, string objectName, Color color, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject element = new GameObject(objectName, typeof(RectTransform));
        Place(element, parent, anchor, position, size);
        Image image = element.AddComponent<Image>();
        image.sprite = StandardSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    internal static RawImage CreateRawImage(Transform parent, string objectName, Texture texture, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject element = new GameObject(objectName, typeof(RectTransform));
        Place(element, parent, anchor, position, size);
        RawImage rawImage = element.AddComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.raycastTarget = false;
        return rawImage;
    }

    internal static TextMeshProUGUI CreateText(Transform parent, string objectName, string text, float fontSize, Color color,
        Vector2 anchor, Vector2 position, Vector2 size, TextAlignmentOptions alignment)
    {
        GameObject element = new GameObject(objectName, typeof(RectTransform));
        Place(element, parent, anchor, position, size);
        TextMeshProUGUI label = element.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    internal static Button CreateButton(Transform parent, string objectName, string label, float fontSize, Color color,
        Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject element = TMP_DefaultControls.CreateButton(GetTmpResources());
        element.name = objectName;
        Place(element, parent, anchor, position, size);
        element.GetComponent<Image>().color = color;

        TMP_Text buttonLabel = element.GetComponentInChildren<TMP_Text>();
        buttonLabel.text = label;
        buttonLabel.fontSize = fontSize;
        buttonLabel.color = Color.white;
        buttonLabel.fontStyle = FontStyles.Bold;
        SetLayerRecursively(element.transform);
        return element.GetComponent<Button>();
    }

    internal static TMP_InputField CreateInputField(Transform parent, string objectName, string placeholder, float fontSize,
        int characterLimit, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject element = TMP_DefaultControls.CreateInputField(GetTmpResources());
        element.name = objectName;
        Place(element, parent, anchor, position, size);

        TMP_InputField inputField = element.GetComponent<TMP_InputField>();
        inputField.characterLimit = characterLimit;
        inputField.pointSize = fontSize;
        if (inputField.placeholder is TMP_Text placeholderText)
        {
            placeholderText.text = placeholder;
            placeholderText.fontSize = fontSize;
        }

        inputField.textComponent.fontSize = fontSize;
        SetLayerRecursively(element.transform);
        return inputField;
    }

    private static void SetLayerRecursively(Transform root)
    {
        root.gameObject.layer = LayerMask.NameToLayer(UI_LAYER);
        foreach (Transform child in root)
        {
            SetLayerRecursively(child);
        }
    }

    private static TMP_DefaultControls.Resources GetTmpResources()
    {
        return new TMP_DefaultControls.Resources
        {
            standard = StandardSprite,
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>(SPRITE_BACKGROUND),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>(SPRITE_INPUT_FIELD),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>(SPRITE_KNOB),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>(SPRITE_CHECKMARK),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>(SPRITE_DROPDOWN),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>(SPRITE_MASK)
        };
    }
}
