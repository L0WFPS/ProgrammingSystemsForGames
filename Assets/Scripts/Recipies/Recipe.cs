using NUnit.Framework.Interfaces;
using UnityEngine;

[CreateAssetMenu(fileName = "Recipe", menuName = "Scriptable Objects/Recipe")]
public class Recipe : ScriptableObject
{
    [Header("Inputs (order does NOT matter)")]
    public ObjectData inputA;
    public ObjectData inputB;

    [Header("Result")]
    public ObjectData resultItem;
    public float craftSeconds;
}
