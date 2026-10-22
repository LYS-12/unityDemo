using System;
using UnityEngine;

[Serializable]
public class DialogueLine
{
    public string speaker;
    [TextArea(2, 5)] public string text;
    public DialogueLine(string name, string value)
    {
        speaker = name;
        text = value;
    }
}

[CreateAssetMenu(menuName = "Forest Party/Fox Dialogue")]
public class FoxDialogue : ScriptableObject
{
    public string foxName;
    public DialogueLine[] apology;
}
