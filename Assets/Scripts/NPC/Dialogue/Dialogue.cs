using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class DialogueLine
{
    [TextArea(3, 10)]
    public string sentence;
    
    [Header("Animation & Events")]
    [Tooltip("Optional animation trigger name to fire when this sentence starts.")]
    public string animationTrigger; 

    [Header("Audio Settings")]
    [Tooltip("Optional audio clip to play during this sentence (e.g., voice blip, sound effect).")]
    public AudioClip sentenceAudio;

    [Range(0f, 1f)]
    [Tooltip("Volume multiplier for this specific audio clip.")]
    public float audioVolume = 1.0f;
}

[System.Serializable]
public class Dialogue
{
    public string name;
    public DialogueLine[] lines; 
    public Animator characterAnimator;
}
