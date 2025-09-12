using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SpriteWindow : EditorWindow
{
    [MenuItem("Rust/Download Sprites", false, 0)]
    static void DownloadSprites()
    {
        SpriteWindow window = ScriptableObject.CreateInstance<SpriteWindow>();
        window.position = new Rect(Screen.width / 2 + 400, Screen.height / 2, 250, 120);
        window.ShowPopup();
    }

    void OnGUI()
    {
        GUILayout.Space(20);
        EditorGUILayout.LabelField("Once you downloaded it, extract the content and items folder in the assets folder where your scene is located!", EditorStyles.wordWrappedLabel);
        GUILayout.Space(20);
        if (GUILayout.Button("Take me to the download!"))
        {
            this.Close();
            System.Diagnostics.Process.Start("https://drive.google.com/u/0/uc?id=1PkktKU3215z6QIzMAaEzuPr2_58l9POs&export=download");
        }
    }
}