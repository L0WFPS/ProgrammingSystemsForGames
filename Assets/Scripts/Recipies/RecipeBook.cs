using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RecipeBook", menuName = "Scriptable Objects/RecipeBook")]
public class RecipeBook : ScriptableObject
{
    public List<Recipe> recipes = new List<Recipe>();
    private Dictionary<string, Recipe> _map;

    void OnEnable()
    {
        _map = new Dictionary<string, Recipe>();
        foreach (var r in recipes)
        {
            if (r == null || r.inputA == null || r.inputB == null) continue;
            _map[MakeKey(r.inputA, r.inputB)] = r;
        }
    }

    public bool TryGetRecipe(ObjectData a, ObjectData b, out Recipe recipe)
    {
        recipe = null;
        if (a == null || b == null) return false;
        return _map.TryGetValue(MakeKey(a, b), out recipe);
    }

    private string MakeKey(ObjectData a, ObjectData b)
    {
        string idA = string.IsNullOrEmpty(a.itemId) ? a.name : a.itemId;
        string idB = string.IsNullOrEmpty(b.itemId) ? b.name : b.itemId;
        if (string.CompareOrdinal(idA, idB) < 0) return idA + "|" + idB;
        return idB + "|" + idA;
    }
}
