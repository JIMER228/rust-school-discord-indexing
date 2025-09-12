using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class UGUIMenu : Editor
{
    [MenuItem("Rust/Add UI Element/Add Panel", false, 0)]
    [MenuItem("GameObject/Rust/Add UI Element/Add Panel", false, 0)]
    private static void AddPanelMenuItem()
    {
        AddItem("Panel");
    }

    [MenuItem("Rust/Add UI Element/Add Label", false, 0)]
    [MenuItem("GameObject/Rust/Add UI Element/Add Label", false, 0)]
    private static void AddLabelMenuItem()
    {
        AddItem("Label");
    }

    [MenuItem("Rust/Add UI Element/Add Button", false, 0)]
    [MenuItem("GameObject/Rust/Add UI Element/Add Button", false, 0)]
    private static void AddButtonMenuItem()
    {
        AddItem("Button");
    }

    [MenuItem("Rust/Add UI Element/Add Input Field", false, 0)]
    [MenuItem("GameObject/Rust/Add UI Element/Add Input Field", false, 0)]
    private static void AddInputMenuItem()
    {
        AddItem("Input");
    }

    [MenuItem("Rust/Add UI Element/Add Image", false, 0)]
    [MenuItem("GameObject/Rust/Add UI Element/Add Image", false, 0)]
    private static void AddImageItem()
    {
        AddItem("Image");
    }

    [MenuItem("Rust/Export UI", false, 0)]
    [MenuItem("GameObject/Rust/Export UI", false, 0)]
    private static void ExportUIMenuItem()
    {
        ExportUI();
    }

    private static Dictionary<string, string> ImageLibaryExports = new Dictionary<string, string>();
    private static void AddItem(string option)
    {
        switch (option)
        {
            case "Panel":
                GameObject panel = new GameObject();
                RectTransform rect = panel.AddComponent<RectTransform>();
                panel.AddComponent<CanvasRenderer>();
                panel.AddComponent<RawImage>();
                panel.AddComponent<RustUIProperties>();
                panel.name = $"Panel_{UnityEngine.Random.Range(1, 9999)}";
                panel.tag = "Panel";
                panel.transform.SetParent(Selection.activeGameObject.transform);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = new Vector3(1, 1, 1);
                Selection.activeGameObject = panel;
                Undo.RegisterCreatedObjectUndo(panel, "");
                break;
            case "Button":
                GameObject button = new GameObject();
                GameObject buttontext = new GameObject();

                RectTransform bRect = button.AddComponent<RectTransform>();
                button.AddComponent<CanvasRenderer>();
                button.AddComponent<RustUIProperties>();
                Image mImage = button.AddComponent<Image>();
                Button mButton = button.AddComponent<Button>();

                button.name = $"Button_{UnityEngine.Random.Range(1, 9999)}";
                button.tag = "Button";
                mButton.targetGraphic = mImage;
                button.transform.SetParent(Selection.activeGameObject.transform);
                bRect.anchoredPosition = Vector2.zero;
                bRect.localScale = new Vector3(1, 1, 1);

                buttontext.name = $"Label_{UnityEngine.Random.Range(1, 9999)}";
                RectTransform btRect = buttontext.AddComponent<RectTransform>();
                buttontext.AddComponent<CanvasRenderer>();
                Text bText = buttontext.AddComponent<Text>();
                bText.alignment = TextAnchor.MiddleCenter;
                bText.text = "Rust UI Button";
                bText.font = Resources.Load("RobotoCondensed-Regular") as Font;
                bText.color = Color.black;
                buttontext.transform.SetParent(button.transform);
                btRect.anchorMin = new Vector2(0, 0);
                btRect.anchorMax = new Vector2(1, 1);
                btRect.pivot = new Vector2(0.5f, 0.5f);
                btRect.anchoredPosition = Vector2.zero;
                btRect.offsetMin = Vector2.zero;
                btRect.offsetMax = Vector2.zero;
                btRect.localScale = new Vector3(1, 1, 1);
                Selection.activeGameObject = button;
                Undo.RegisterCreatedObjectUndo(button, "");

                break;
            case "Label":
                GameObject label = new GameObject();

                label.name = $"Label_{UnityEngine.Random.Range(1, 9999)}";
                label.tag = "Label";
                RectTransform lRect = label.AddComponent<RectTransform>();
                label.AddComponent<CanvasRenderer>();
                label.AddComponent<RustUIProperties>();
                label.AddComponent<Outline>();
                Text lText = label.AddComponent<Text>();
                lText.text = "Rust UI Label";
                lText.font = Resources.Load("RobotoCondensed-Regular") as Font;
                label.transform.SetParent(Selection.activeGameObject.transform);
                lRect.anchoredPosition = Vector2.zero;
                lRect.localScale = new Vector3(1, 1, 1);
                Selection.activeGameObject = label;
                Undo.RegisterCreatedObjectUndo(label, "");

                break;
            case "Input":
                GameObject inputfield = new GameObject();
                GameObject inputplaceholder = new GameObject();
                GameObject inputtext = new GameObject();

                RectTransform iRect = inputfield.AddComponent<RectTransform>();
                inputfield.AddComponent<CanvasRenderer>();
                inputfield.AddComponent<RustUIProperties>();
                Image iImage = inputfield.AddComponent<Image>();
                InputField iField = inputfield.AddComponent<InputField>();

                inputplaceholder.name = $"Placeholder_{UnityEngine.Random.Range(1, 9999)}";
                RectTransform ipRect = inputplaceholder.AddComponent<RectTransform>();
                inputplaceholder.AddComponent<CanvasRenderer>();
                Text iPText = inputplaceholder.AddComponent<Text>();
                iPText.text = "Enter Text Here...";
                iPText.fontStyle = FontStyle.Italic;
                iPText.color = Color.grey;
                iPText.alignment = TextAnchor.MiddleLeft;
                iPText.font = Resources.Load("RobotoCondensed-Regular") as Font;
                iPText.transform.position = new Vector3(0, 0, 0);

                inputtext.name = $"InputText_{UnityEngine.Random.Range(1, 9999)}";
                RectTransform itRect = inputtext.AddComponent<RectTransform>();
                inputtext.AddComponent<CanvasRenderer>();
                Text iText = inputtext.AddComponent<Text>();
                iText.color = Color.black;
                iText.alignment = TextAnchor.MiddleLeft;
                iText.transform.position = new Vector3(0, 0, 0);

                inputfield.name = $"InputField_{UnityEngine.Random.Range(1, 9999)}";
                inputfield.tag = "Input";
                inputfield.transform.SetParent(Selection.activeGameObject.transform);
                iRect.anchoredPosition = Vector2.zero;
                iRect.localScale = new Vector3(1, 1, 1);
                iField.targetGraphic = iImage;
                iField.textComponent = inputtext.GetComponent<Text>();
                iField.placeholder = inputplaceholder.GetComponent<Text>();

                iText.transform.SetParent(inputfield.transform);
                iPText.transform.SetParent(inputfield.transform);
                itRect.anchorMin = new Vector2(0, 0);
                itRect.anchorMax = new Vector2(1, 1);
                itRect.pivot = new Vector2(0.5f, 0.5f);
                itRect.anchoredPosition = Vector2.zero;
                itRect.offsetMin = Vector2.zero;
                itRect.offsetMax = Vector2.zero;
                itRect.localScale = new Vector3(1, 1, 1);

                ipRect.anchorMin = new Vector2(0, 0);
                ipRect.anchorMax = new Vector2(1, 1);
                ipRect.pivot = new Vector2(0.5f, 0.5f);
                ipRect.anchoredPosition = Vector2.zero;
                ipRect.offsetMin = Vector2.zero;
                ipRect.offsetMax = Vector2.zero;
                ipRect.localScale = new Vector3(1, 1, 1);
                Selection.activeGameObject = inputfield;
                Undo.RegisterCreatedObjectUndo(inputfield, "");
                break;

            case "Image":
                GameObject image = new GameObject();
                RectTransform imRect = image.AddComponent<RectTransform>();
                image.AddComponent<CanvasRenderer>();
                image.AddComponent<RawImage>();
                image.AddComponent<RustUIProperties>();
                image.AddComponent<Outline>();
                image.name = $"Image_{UnityEngine.Random.Range(1, 9999)}";
                image.tag = "Image";
                image.transform.SetParent(Selection.activeGameObject.transform);
                imRect.anchoredPosition = Vector2.zero;
                imRect.localScale = new Vector3(1, 1, 1);
                Selection.activeGameObject = image;
                Undo.RegisterCreatedObjectUndo(image, "");
                break;
        }
    }

    private static void ExportUI()
    {
        ImageLibaryExports.Clear();
        Debug.Log("<color=orange>Starting Export</color>");

        GameObject UI = Selection.activeGameObject;
        string path = Application.dataPath + "\\" + "Exports\\";
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        string fullpath = path + UI.name + ".txt";
        if (File.Exists(fullpath))
            File.Delete(fullpath);

        StringBuilder fullexport = new StringBuilder();
        fullexport.Append($"private void {UI.name + "(BasePlayer player) {"}\nvar container = new CuiElementContainer();\n");

        foreach (Transform g in UI.transform.GetComponentsInChildren<Transform>())
        {
            GameObject child = g.gameObject;
            if (!child.activeInHierarchy) continue;

            AddElement(child, fullexport);
        }

        fullexport.Append($"CuiHelper.DestroyUI(player, \"{UI.name}\");");
        fullexport.Append($"\nCuiHelper.AddUi(player, container);\n{"}"}");

        if (ImageLibaryExports.Count > 0)
        {
            int index = 0;
            fullexport.Append($"\n\n[PluginReference] private Plugin ImageLibrary;\nvoid Loaded() {"{"}");
            foreach (var name in ImageLibaryExports.Keys)
            {
                fullexport.Append($"\n\t\t\tImageLibrary?.Call(\"AddImage\",\"{ImageLibaryExports.Values.ElementAt(index)}\",{name});");
                index++;
            }
            fullexport.Append($"\n{"};"}");
        }

        File.WriteAllText(fullpath, fullexport.ToString());
        Debug.Log("<color=green>UI Export Finished</color>");
    }

    private static StringBuilder AddElement(GameObject element, StringBuilder export)
    {
        float anchorMinX = (float)Math.Round(element.GetComponent<RectTransform>().anchorMin.x, 3); float anchorMinY = (float)Math.Round(element.GetComponent<RectTransform>().anchorMin.y, 3); float anchorMaxX = (float)Math.Round(element.GetComponent<RectTransform>().anchorMax.x, 3); float anchorMaxY = (float)Math.Round(element.GetComponent<RectTransform>().anchorMax.y, 3);
        float offsetMinX = (float)Math.Round(element.GetComponent<RectTransform>().offsetMin.x, 3); float offsetMinY = (float)Math.Round(element.GetComponent<RectTransform>().offsetMin.y, 3); float offsetMaxX = (float)Math.Round(element.GetComponent<RectTransform>().offsetMax.x, 3); float offsetMaxY = (float)Math.Round(element.GetComponent<RectTransform>().offsetMax.y, 3);
        string elementAnchor = string.Format("\"{0} {1}|{2} {3}\"", anchorMinX.ToString().Replace(",", "."), anchorMinY.ToString().Replace(",", "."), anchorMaxX.ToString().Replace(",", "."), anchorMaxY.ToString().Replace(",", "."));
        string elementOffset = string.Format("\"{0} {1}|{2} {3}\"", offsetMinX.ToString().Replace(",", "."), offsetMinY.ToString().Replace(",", "."), offsetMaxX.ToString().Replace(",", "."), offsetMaxY.ToString().Replace(",", "."));

        string parent = string.Empty;
        if (element.transform.parent != null)
        {
            if (element.transform.parent.gameObject.name == "RustUI")
                parent = "\"Overlay\"";
            else
                parent = $"\"{element.transform.parent.gameObject.name}\"";
        }

        float fadeIn = 0f; float fadeOut = 0f; string cursor = "false"; string buttonCMD = "\"\""; string inputCMD = "\"\""; bool useImageLibary = false; bool exportChildLabels = false; RustUIProperties.RustUIListType buttonListType = RustUIProperties.RustUIListType.None;
        if (element.GetComponent<RustUIProperties>())
        {
            buttonCMD = element.GetComponent<RustUIProperties>().ButtonCommand;
            inputCMD = element.GetComponent<RustUIProperties>().InputCommand;
            cursor = element.GetComponent<RustUIProperties>().CursorEnabled.ToString().ToLower();
            fadeIn = element.GetComponent<RustUIProperties>().FadeIn;
            fadeOut = element.GetComponent<RustUIProperties>().FadeOut;
            useImageLibary = element.GetComponent<RustUIProperties>().UseImageLibrary;
            buttonListType = element.GetComponent<RustUIProperties>().ListType;
        }

        string elementColor, elementText, elementSprite = "\"\"";
        string elementURL = "\"\"";
        string elementMaterial = "\"\"";
        string elementName = $"\"{element.name}\"";
        string elementFont = "\"robotocondensed-bold.ttf\"";
        string elementTextColor = "\"1 1 1 1\"";
        string elementOutlineColor = "\"0 0 0 0\"";
        string elementOutlineDistance = "\"0 0\"";
        int elementFontSize = 20;
        int elementAligment = 0;

        if (element.GetComponent<RawImage>())
        {
            elementMaterial = GetMaterial(element.GetComponent<RawImage>().material.ToString().Replace(" (UnityEngine.Material)", ""));
            string imageName = element?.GetComponent<RawImage>()?.texture?.ToString();
            if (imageName != "null")
            {
                elementSprite = $"\"{CheckIfRustSprite(imageName.Replace(" (UnityEngine.Texture2D)", ""))}\"";

                if (elementSprite == "\"\"")
                {
                    string imageDir = GetImageDirectory(imageName.Replace(" (UnityEngine.Texture2D)", "")).Replace(@"\", "/");
                    elementURL = $"\"{PostToImgur(imageDir)}\"";
                }
            }
        }

        if (element.GetComponent<Outline>())
        {
            elementOutlineColor = string.Format("\"{0} {1} {2} {3}\"", element.GetComponent<Outline>().effectColor.r.ToString().Replace(",", "."), element.GetComponent<Outline>().effectColor.g.ToString().Replace(",", "."), element.GetComponent<Outline>().effectColor.b.ToString().Replace(",", "."), element.GetComponent<Outline>().effectColor.a.ToString().Replace(",", "."));
            elementOutlineDistance = string.Format("\"{0} {1}\"", element.GetComponent<Outline>().effectDistance.x.ToString().Replace(",", "."), element.GetComponent<Outline>().effectDistance.y.ToString().Replace(",", "."));
        }

        string isSpriteLegecy = string.Format("{0}", elementSprite == "\"\"" ? "" : $", Sprite = {elementSprite}");
        string isMaterialLegecy = string.Format("{0}", elementMaterial == "\"\"" ? "" : $", Material = {elementMaterial}");
        string isFadeInLegecy = string.Format("{0}", fadeIn == 0 ? "" : $", FadeIn = {fadeIn}");
        string isFadeOutLegecy = string.Format("{0}", fadeOut == 0 ? "" : $"\n\tFadeOut = {fadeOut},");
        string isButtonCommandLegecy = string.Format("{0}", buttonCMD == "\"\"" ? "" : $", Command = {buttonCMD}");
        string isUrlLegecy = string.Format("{0}", elementURL == "\"\"" ? "" : $", Url = {elementURL}");
        string isInputCommandLegecy = string.Format("{0}", inputCMD == "\"\"" ? "" : $", Command = {inputCMD}");
        string outlineFormat = $"\n\t\t\t\t\tnew CuiOutlineComponent {"{"} Color = {elementOutlineColor}, Distance = {elementOutlineDistance} {"}"},";
        string isOutlineLegecy = string.Format("{0}", !element.GetComponent<Outline>() || element.GetComponent<Outline>().effectColor.a == 0 ? "" : outlineFormat);

        switch (element.tag)
        {
            default:
                break;

            case "Panel":
                if (element.GetComponent<Image>()) Debug.LogError($"Must have RawImage component, image component no longer is supported. Element: ({element.name})");
                elementColor = string.Format("\"{0} {1} {2} {3}\"", element.GetComponent<RawImage>().color.r.ToString().Replace(",", "."), element.GetComponent<RawImage>().color.g.ToString().Replace(",", "."), element.GetComponent<RawImage>().color.b.ToString().Replace(",", "."), element.GetComponent<RawImage>().color.a.ToString().Replace(",", "."));

                if (elementURL != "")
                {
                    if (useImageLibary)
                    {
                        isSpriteLegecy = $", Png = ImageLibrary?.Call<string>(\"GetImage\",{elementName})";
                        if (!ImageLibaryExports.ContainsKey(elementName)) ImageLibaryExports.Add(element.name, elementURL);
                    };
                }

                if (buttonListType != RustUIProperties.RustUIListType.None)
                {
                    export.Append($"container.Add(new CuiPanel\n{"{"}\n\tCursorEnabled = {cursor},{isFadeOutLegecy}\n\tImage = {"{"} Color = {elementColor}{isFadeInLegecy}{isSpriteLegecy}{isMaterialLegecy} {"}"},\n\tRectTransform ={"{"} AnchorMin = {elementAnchor.Split('|')[0]}\", AnchorMax = \"{elementAnchor.Split('|')[1]}, OffsetMin = {elementOffset.Split('|')[0]}\", OffsetMax = \"{elementOffset.Split('|')[1]} {"}"}\n{"}"},{parent},{elementName});\n\n");
                    SetupButtons(export, buttonListType, element.GetComponent<RustUIProperties>());
                }
                else
                    export.Append($"container.Add(new CuiPanel\n{"{"}\n\tCursorEnabled = {cursor},{isFadeOutLegecy}\n\tImage = {"{"} Color = {elementColor}{isFadeInLegecy}{isSpriteLegecy}{isMaterialLegecy} {"}"},\n\tRectTransform ={"{"} AnchorMin = {elementAnchor.Split('|')[0]}\", AnchorMax = \"{elementAnchor.Split('|')[1]}, OffsetMin = {elementOffset.Split('|')[0]}\", OffsetMax = \"{elementOffset.Split('|')[1]} {"}"}\n{"}"},{parent},{elementName});\n\n");
                break;

            case "Image":
                if (element.GetComponent<Image>()) Debug.LogError($"Must have RawImage component, image component no longer is supported. Element: ({element.name})");
                elementColor = string.Format("\"{0} {1} {2} {3}\"", element.GetComponent<RawImage>().color.r.ToString().Replace(",", "."), element.GetComponent<RawImage>().color.g.ToString().Replace(",", "."), element.GetComponent<RawImage>().color.b.ToString().Replace(",", "."), element.GetComponent<RawImage>().color.a.ToString().Replace(",", "."));
                elementMaterial = GetMaterial(element.GetComponent<RawImage>().material.ToString().Replace(" (UnityEngine.Material)", ""));

                if (!element.GetComponent<Outline>())

                    if (elementColor != "1 1 1 1") elementMaterial = "\"assets/icons/iconmaterial.mat\"";

                if (elementURL != "")
                {
                    if (useImageLibary)
                    {
                        isUrlLegecy = $", Png = ImageLibrary?.Call<string>(\"GetImage\",{elementName})";
                        if (!ImageLibaryExports.ContainsKey(elementName)) ImageLibaryExports.Add(element.name, elementURL);
                    };
                }

                export.Append($"container.Add(new CuiElement\n{"{"}\n\tName = {elementName},\n\tParent = {parent},{isFadeOutLegecy}\n\tComponents = {"{"}\n\t\t\t\t\tnew CuiRawImageComponent {"{"} Color = {elementColor}{isFadeInLegecy}{isSpriteLegecy}{isMaterialLegecy}{isUrlLegecy} {"}"},{isOutlineLegecy}\n\t\t\t\t\tnew CuiRectTransformComponent {"{"} AnchorMin = {elementAnchor.Split('|')[0]}\", AnchorMax = \"{elementAnchor.Split('|')[1]}, OffsetMin = {elementOffset.Split('|')[0]}\", OffsetMax = \"{elementOffset.Split('|')[1]} {"}"}\n\t\t\t\t{"}"}\n{"}"});\n\n");
                break;

            case "Button":
                if (!element.GetComponentInChildren<Text>()) Debug.LogError($"Must have a label as a child on the button. Element: ({element.name})");
                elementColor = string.Format("\"{0} {1} {2} {3}\"", element.GetComponent<Button>().colors.normalColor.r.ToString().Replace(",", "."), element.GetComponent<Button>().colors.normalColor.g.ToString().Replace(",", "."), element.GetComponent<Button>().colors.normalColor.b.ToString().Replace(",", "."), element.GetComponent<Button>().colors.normalColor.a.ToString().Replace(",", "."));
                elementText = string.Format("\"{0}\"", element.GetComponentInChildren<Text>().text);
                elementTextColor = string.Format("\"{0} {1} {2} {3}\"", element.GetComponentInChildren<Text>().color.r.ToString().Replace(",", "."), element.GetComponentInChildren<Text>().color.g.ToString().Replace(",", "."), element.GetComponentInChildren<Text>().color.b.ToString().Replace(",", "."), element.GetComponentInChildren<Text>().color.a.ToString().Replace(",", "."));
                elementFont = string.Format("\"{0}\"", GetFont(element.GetComponentInChildren<Text>().font.ToString()));
                elementFontSize = element.GetComponentInChildren<Text>().fontSize;
                elementAligment = (int)element.GetComponentInChildren<Text>().alignment;
                string textFormat = $"\n\tText = {"{"} Text = {elementText}, Font = {elementFont}, FontSize = {elementFontSize}, Align = TextAnchor.{(TextAnchor)elementAligment}, Color = {elementTextColor}{isFadeInLegecy} {"}"},";
                string isTextLegecy = string.Format("{0}", elementText == "\"\"" ? "" : textFormat);

                export.Append($"container.Add(new CuiButton\n{"{"}{isFadeOutLegecy}\n\tButton = {"{"} Color = {elementColor}{isFadeInLegecy}{isSpriteLegecy}{isMaterialLegecy}{isButtonCommandLegecy} {"}"},{isTextLegecy}\n\tRectTransform = {"{"} AnchorMin = {elementAnchor.Split('|')[0]}\", AnchorMax = \"{elementAnchor.Split('|')[1]}, OffsetMin = {elementOffset.Split('|')[0]}\", OffsetMax = \"{elementOffset.Split('|')[1]} {"}"}\n{"}"},{parent},{elementName});\n\n");
                break;

            case "Label":
                if (element.GetComponentInParent<Button>()) exportChildLabels = element.GetComponentInParent<Button>().GetComponent<RustUIProperties>().ExportChildLables;
                if (!exportChildLabels && element.GetComponentInParent<Button>()) return new StringBuilder("");


                elementColor = string.Format("\"{0} {1} {2} {3}\"", element.GetComponent<Text>().color.r.ToString().Replace(",", "."), element.GetComponent<Text>().color.g.ToString().Replace(",", "."), element.GetComponent<Text>().color.b.ToString().Replace(",", "."), element.GetComponent<Text>().color.a.ToString().Replace(",", "."));
                elementText = string.Format("\"{0}\"", element.GetComponent<Text>().text);
                elementFont = string.Format("\"{0}\"", GetFont(element.GetComponent<Text>().font.ToString()));
                elementFontSize = element.GetComponent<Text>().fontSize;
                elementAligment = (int)element.GetComponent<Text>().alignment;
                export.Append($"container.Add(new CuiElement\n{"{"}\n\tName = {elementName},\n\tParent = {parent},{isFadeOutLegecy}\n\tComponents = {"{"}\n\t\t\t\t\tnew CuiTextComponent {"{"} Text = {elementText}, Font = {elementFont}, FontSize = {elementFontSize}, Align = TextAnchor.{(TextAnchor)elementAligment}, Color = {elementColor}{isFadeInLegecy} {"}"},{isOutlineLegecy}\n\t\t\t\t\tnew CuiRectTransformComponent {"{"} AnchorMin = {elementAnchor.Split('|')[0]}\", AnchorMax = \"{elementAnchor.Split('|')[1]}, OffsetMin = {elementOffset.Split('|')[0]}\", OffsetMax = \"{elementOffset.Split('|')[1]} {"}"}\n\t\t\t\t{"}"}\n{"}"});\n\n");
                break;

            case "Input":
                elementColor = string.Format("\"{0} {1} {2} {3}\"", element.GetComponent<InputField>().colors.normalColor.r.ToString().Replace(",", "."), element.GetComponent<InputField>().colors.normalColor.g.ToString().Replace(",", "."), element.GetComponent<InputField>().colors.normalColor.b.ToString().Replace(",", "."), element.GetComponent<InputField>().colors.normalColor.a.ToString().Replace(",", "."));
                elementFont = string.Format("\"{0}\"", GetFont(element.GetComponentInChildren<Text>().font.ToString()));
                elementFontSize = element.GetComponentInChildren<Text>().fontSize;
                bool isPassword = element.GetComponent<InputField>().contentType.ToString() == "Password";
                int charLimit = element.GetComponent<InputField>().characterLimit;

                export.Append($"container.Add(new CuiElement\n{"{"}\n\tName = {elementName},\n\tParent = {parent},{isFadeOutLegecy}\n\tComponents = {"{"}\n\t\t\t\t\tnew CuiInputFieldComponent {"{"} Color = {elementColor}, Font = {elementFont}, FontSize = {elementFontSize}, Align = TextAnchor.{(TextAnchor)elementAligment}, CharsLimit = {charLimit}, IsPassword = {isPassword.ToString().ToLower()}{isInputCommandLegecy}{isFadeInLegecy} {"}"},\n\t\t\t\t\tnew CuiRectTransformComponent {"{"} AnchorMin = {elementAnchor.Split('|')[0]}\", AnchorMax = \"{elementAnchor.Split('|')[1]}, OffsetMin = {elementOffset.Split('|')[0]}\", OffsetMax = \"{elementOffset.Split('|')[1]} {"}"}\n\t\t\t\t{"}"}\n{"}"});\n\n");
                break;
        }

        return export;
    }

    private static string GetMaterial(string material)
    {
        string directory = "\"\"";
        switch (material)
        {
            case "uibackgroundblur":
                directory = "\"assets/content/ui/uibackgroundblur.mat\"";
                break;

            case "uibackgroundblur-notice":
                directory = "\"assets/content/ui/uibackgroundblur-notice.mat\"";
                break;

            case "uibackgroundblur-ingamemenu":
                directory = "\"assets/content/ui/uibackgroundblur-ingamemenu.mat\"";
                break;

            default:
                directory = "\"\"";
                break;
        }
        return directory;
    }

    private static string GetDirectory()
    {
        return Application.streamingAssetsPath.Replace("StreamingAssets", "");
    }

    private static string FinalizeDirectory(string directory)
    {
        string basePath = GetDirectory();
        string newString = directory.Replace(basePath, "assets/").Replace(@"\", "/");
        return newString;
    }

    private static string GetImageDirectory(string imgName)
    {

        string folderPath = GetDirectory();
        List<string> imageList = Directory.GetFiles(folderPath, "*.png*", SearchOption.AllDirectories).ToList();

        int index = imageList.FindIndex(x => x.EndsWith($"{imgName + ".png"}"));
        return imageList[index];
    }

    private static string CheckIfRustSprite(string imgName)
    {
        string folderPath = GetDirectory();

        var allowedExtensions = new[] { ".png", ".tga", ".psd" };
        List<string> imageList = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).Where(file => allowedExtensions.Any(file.ToLower().EndsWith)).ToList();

        int index = -1;
        index = imageList.FindIndex(x => x.EndsWith($"{imgName + ".png"}") || x.EndsWith($"{imgName + ".tga"}") || x.EndsWith($"{imgName + ".psd"}"));
        if (index < 0) { Debug.LogError("Only PNGs, TGAs, and PSDs are supported for export, please check your images file extension and try again"); return ""; }
        if (!imageList[index].Contains("content") && !imageList[index].Contains("icons")) return "";

        return FinalizeDirectory(imageList[index]);
    }

    public static string PostToImgur(string imgFilePath)
    {
        using (var w = new WebClient())
        {
            string clientID = File.ReadAllText(GetDirectory() + "ImgurToken.txt");
            if (clientID.Length > 20) { Debug.LogError("You need to have a imgur token setup in order to upload custom images. Follow the guide in the ImgurToken.txt file."); };

            w.Headers.Add("Authorization", "Client-ID " + clientID);
            var values = new NameValueCollection
            {
                { "image", Convert.ToBase64String(File.ReadAllBytes(imgFilePath)) }
            };

            byte[] response = w.UploadValues("https://api.imgur.com/3/upload.xml", values);

            var doc = XDocument.Load(new MemoryStream(response));
            string imageUrl = (string)doc.Root.Element("link");
            return imageUrl;
        }
    }

    private static string GetFont(string font)
    {
        switch (font)
        {
            case "DroidSansMono (UnityEngine.Font)":
                return "droidsansmono.ttf";
            case "PermanentMarker (UnityEngine.Font)":
                return "permanentmarker.ttf";
            case "RobotoCondensed-Regular (UnityEngine.Font)":
                return "robotocondensed-regular.ttf";
            case "RobotoCondensed-Bold (UnityEngine.Font)":
                return "robotocondensed-bold.ttf";
            default:
                return "Font Not Supported";
        }
    }

    private static void SetupButtons(StringBuilder export, RustUIProperties.RustUIListType listType, RustUIProperties uiProps)
    {
        string horizontalButtonDistance = string.Empty; string verticalButtonDistance = string.Empty;
        float offsetMinVertDis, offsetMaxVertDis, offsetMinHorzDis, offsetMaxHorzDis = 0f;
        GameObject primaryButton = uiProps.primaryButton; GameObject verticalButton = uiProps.verticalButton; GameObject horizontalButton = uiProps.horizontalButton;

        string elementColor = string.Format("\"{0} {1} {2} {3}\"", primaryButton.GetComponent<Button>().colors.normalColor.r.ToString().Replace(",", "."), primaryButton.GetComponent<Button>().colors.normalColor.g.ToString().Replace(",", "."), primaryButton.GetComponent<Button>().colors.normalColor.b.ToString().Replace(",", "."), primaryButton.GetComponent<Button>().colors.normalColor.a.ToString().Replace(",", "."));
        string elementText = string.Format("\"{0}\"", primaryButton.GetComponentInChildren<Text>().text);
        string elementTextColor = string.Format("\"{0} {1} {2} {3}\"", primaryButton.GetComponentInChildren<Text>().color.r.ToString().Replace(",", "."), primaryButton.GetComponentInChildren<Text>().color.g.ToString().Replace(",", "."), primaryButton.GetComponentInChildren<Text>().color.b.ToString().Replace(",", "."), primaryButton.GetComponentInChildren<Text>().color.a.ToString().Replace(",", "."));
        string elementFont = string.Format("\"{0}\"", GetFont(primaryButton.GetComponentInChildren<Text>().font.ToString()));
        int elementFontSize = primaryButton.GetComponentInChildren<Text>().fontSize;
        int elementAligment = (int)primaryButton.GetComponentInChildren<Text>().alignment;
        string isFadeInLegecy = string.Format("{0}", primaryButton.GetComponent<RustUIProperties>().FadeIn == 0 ? "" : $", FadeIn = {primaryButton.GetComponent<RustUIProperties>().FadeIn}");
        string isFadeOutLegecy = string.Format("{0}", primaryButton.GetComponent<RustUIProperties>().FadeOut == 0 ? "" : $"\n\tFadeOut = {primaryButton.GetComponent<RustUIProperties>().FadeOut}");
        string isButtonCommandLegecy = string.Format("{0}", primaryButton.GetComponent<RustUIProperties>().ButtonCommand == "\"\"" ? "" : $", Command = {primaryButton.GetComponent<RustUIProperties>().ButtonCommand}");
        string buttonTextFormat = $"\n\t\tText = {"{"} Text = {elementText}, Font = {elementFont}, FontSize = {elementFontSize}, Align = TextAnchor.{(TextAnchor)elementAligment}, Color = {elementTextColor}{isFadeInLegecy} {"}"},";
        string isTextLegecyButton = string.Format("{0}", elementText == "\"\"" ? "" : buttonTextFormat);

        switch (listType)
        {
            case RustUIProperties.RustUIListType.Vertical:
                if (primaryButton == null || verticalButton == null) { Debug.LogError("You need to fill out the primary & vertical button fields, under the list options in Rust UI Properties component!"); return; }

                verticalButtonDistance = GetDistance(uiProps.primaryButton, uiProps.verticalButton);
                if (string.IsNullOrEmpty(verticalButtonDistance)) { Debug.LogError("Error getting distance between buttons."); return; }

                offsetMinVertDis = float.Parse(verticalButtonDistance.Split('|')[0]);
                offsetMaxVertDis = float.Parse(verticalButtonDistance.Split('|')[1]);

                export.Append($"float minx = {primaryButton.GetComponent<RectTransform>().offsetMin.x}f;\nfloat maxx = {primaryButton.GetComponent<RectTransform>().offsetMax.x}f; float miny = {primaryButton.GetComponent<RectTransform>().offsetMin.y}f;\nfloat maxy = {primaryButton.GetComponent<RectTransform>().offsetMax.y}f;\nfor (int i = 0; i < option.Count; i++) { "{"}\n\tif (i != 0) { "{"}\n\t\tminy -= {offsetMinVertDis}f;\n\t\tmaxy -= {offsetMaxVertDis}f;\n\t{"}"}\n\n");
                break;

            case RustUIProperties.RustUIListType.Horizontal:
                if (primaryButton == null || horizontalButton == null) { Debug.LogError("You need to fill out the primary & horizontal button fields, under the list options in Rust UI Properties component!"); return; }

                offsetMinHorzDis = float.Parse(horizontalButtonDistance.Split('|')[0]);
                offsetMaxHorzDis = float.Parse(horizontalButtonDistance.Split('|')[1]);

                export.Append($"float minx = {primaryButton.GetComponent<RectTransform>().offsetMin.x}f;\nfloat maxx = {primaryButton.GetComponent<RectTransform>().offsetMax.x}f;\nfor (int i = 0; i < option.Count; i++) { "{"}\n\tif (i != 0) { "{"}\n\t\tminx -= {offsetMinHorzDis}f;\n\t\tmaxx -= {offsetMaxHorzDis}f;\n\t{"}"}\n\n");
                break;

            case RustUIProperties.RustUIListType.Both:
                if (primaryButton == null || horizontalButton == null || verticalButton == null) { Debug.LogError("You need to fill out the buttons, under the list options in Rust UI Properties component!"); return; }

                verticalButtonDistance = GetDistance(uiProps.primaryButton, uiProps.verticalButton);
                horizontalButtonDistance = GetDistance(uiProps.primaryButton, uiProps.horizontalButton, false);
                if (horizontalButtonDistance == "" || verticalButtonDistance == "") return;

                offsetMinVertDis = float.Parse(verticalButtonDistance.Split('|')[0]);
                offsetMaxVertDis = float.Parse(verticalButtonDistance.Split('|')[1]);
                offsetMinHorzDis = float.Parse(horizontalButtonDistance.Split('|')[0]);
                offsetMaxHorzDis = float.Parse(horizontalButtonDistance.Split('|')[1]);

                List<int> resetIndexs = new List<int>();
                for (int i = 1; i < uiProps.maxButtonsPerPage; i++)
                    if (i % uiProps.numberOfButtonsPerRow == 0) resetIndexs.Add(i);

                int[] resetIndexsArray = resetIndexs.ToArray();
                string result = string.Join(",", resetIndexsArray);
                export.Append($"float minx = {primaryButton.GetComponent<RectTransform>().offsetMin.x}f;\nfloat maxx = {primaryButton.GetComponent<RectTransform>().offsetMax.x}f;\nfloat miny = {primaryButton.GetComponent<RectTransform>().offsetMin.y}f;\nfloat maxy = {primaryButton.GetComponent<RectTransform>().offsetMax.y}f;\nfor (int i = 0; i < option.Count; i++) { "{"}\n\tif (i != 0) {"{"}\n\t\tint[] resetIndexs = {"{"}{result}{"}"};\n\t\tif (resetIndexs.Contains(i)){"{"}\n\t\t\tminx = {primaryButton.GetComponent<RectTransform>().offsetMin.x}f;\n\t\t\tmaxx = {primaryButton.GetComponent<RectTransform>().offsetMax.x}f;\n\t\t\tminy -= {offsetMinVertDis}f;\n\t\t\tmaxy -= {offsetMaxVertDis}f;\n\t\t{"}"} else {"{"}\n\t\t\tminx += {offsetMinHorzDis}f;\n\t\t\tmaxx += {offsetMaxHorzDis}f;\n\t\t{"}"}\n\t{"}"}\n\n");
                break;
        }

        export.Append($"\tcontainer.Add(new CuiButton\n\t{"{"}{isFadeOutLegecy}\n\t\tButton = {"{"} Color = {elementColor}{isFadeInLegecy}{isButtonCommandLegecy} {"}"},{isTextLegecyButton}\n\t\tRectTransform = {"{"} AnchorMin = \"{primaryButton.GetComponent<RectTransform>().anchorMin.x.ToString().Replace(",", ".")} {primaryButton.GetComponent<RectTransform>().anchorMin.y.ToString().Replace(",", ".")}\", AnchorMax = \"{primaryButton.GetComponent<RectTransform>().anchorMax.x.ToString().Replace(",", ".")} {primaryButton.GetComponent<RectTransform>().anchorMax.y.ToString().Replace(",", ".")}\", OffsetMin = $\"{"{minx} {miny}"}\", OffsetMax =  $\"{"{maxx} {maxy}"}\" {"}"}\n\t{"}"},{$"\"{primaryButton.transform.parent.gameObject.name}\""},\"{primaryButton.name}\");\n{"}"}\n\n");
    }

    private static string GetDistance(GameObject Button1 = null, GameObject Button2 = null, bool vertical = true)
    {
        if (Button1 == null || Button2 == null) { Debug.LogError("There must be 2 objects selected for this to work."); return ""; };

        Debug.Log("here");
        float offsetMin, offsetMax = 0f;
        if (vertical)
        {
            offsetMin = Button1.GetComponent<RectTransform>().offsetMin.y - Button2.GetComponent<RectTransform>().offsetMin.y;
            Debug.Log(offsetMin.ToString());
            offsetMax = Button1.GetComponent<RectTransform>().offsetMax.y - Button2.GetComponent<RectTransform>().offsetMax.y;
            Debug.Log(offsetMax.ToString());
        }
        else
        {
            offsetMin = Button1.GetComponent<RectTransform>().offsetMin.x - Button2.GetComponent<RectTransform>().offsetMin.x;
            offsetMax = Button1.GetComponent<RectTransform>().offsetMax.x - Button2.GetComponent<RectTransform>().offsetMax.x;
        }




        return $"{(float)Math.Round(offsetMin, 3)}|{(float)Math.Round(offsetMax, 3)}";
    }
}