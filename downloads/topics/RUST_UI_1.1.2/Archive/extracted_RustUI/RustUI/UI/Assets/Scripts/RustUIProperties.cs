using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class RustUIProperties : MonoBehaviour
{
    public enum RustUIListType { None, Vertical, Horizontal, Both };

    [Header("All Elements")]
    public float FadeIn = 0f;
    public float FadeOut = 0f;

    [Header("Panel")]
    public bool CursorEnabled = false;

    [Header("Panel & Image")]
    public bool UseImageLibrary = false;

    [Header("Button")]
    public string ButtonCommand = "\"\"";
    public bool ExportChildLables = false;

    [Header("Input Field")]
    public string InputCommand = "\"\"";

    [Header("Button List")]
    public RustUIListType ListType = RustUIListType.None;
    public GameObject primaryButton = null;
    public GameObject horizontalButton = null;
    public GameObject verticalButton = null;
    public int numberOfButtonsPerRow = 0;
    public int maxButtonsPerPage = 0;
}
