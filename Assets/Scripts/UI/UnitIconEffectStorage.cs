using System;
using System.Xml.Serialization;
using UnityEngine;

/// <summary>
/// Effect Data of ingame running, applied to player or other character
/// </summary>
[CreateAssetMenu(fileName = "UnitIconEffectStorage", menuName = "Scriptable Objects/UnitIconEffectStorage")]
public class IngameEffectIconStorage : ScriptableObject
{
    [Header("Icon List")]
    //[ArrayElementTitle("Icon List")]
    public IngameEffectIcon[] iconArray;
}

[Serializable]
public class IngameEffectIcon
{
    [SerializeReference, SubclassSelector] public IngameEffectData behavior;
    public Sprite sprite;
    public Color color;
}