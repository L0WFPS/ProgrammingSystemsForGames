using NUnit.Framework.Interfaces;
using UnityEngine;
using System.Collections;


public class CraftingProcess : MonoBehaviour
{
    [Header("Recipe source")]
    public RecipeBook recipeBook;

    public CraftingTable ct;

    [Header("Inputs (choose one approach)")]
    public ObjectData inputAData;         // set if you pass ItemData directly
    public ObjectData inputBData;         // set if you pass ItemData directly
    public GameObject inputAObject;     // set if you pass world objects (with Ingredient)
    public GameObject inputBObject;     // set if you pass world objects (with Ingredient)

    public GameObject outputObject;

    [Header("Where to spawn the product")]
    public Transform outputPoint;

    [Header("Runtime")]
    public bool isCrafting;

    public void BeginCraft()
    {
        Debug.Log("BeginCraft");
        if (isCrafting) return;

        inputAObject = ct.slot1Obj;
        inputBObject = ct.slot2Obj;

        // Resolve ItemData from whichever way you provided inputs
        var a = ResolveItemData(inputAData, inputAObject);
        var b = ResolveItemData(inputBData, inputBObject);

        if (a == null || b == null)
        {
            Debug.LogWarning("CraftingProcessor: Missing inputs.");
            return;
        }

        if (recipeBook != null && recipeBook.TryGetRecipe(a, b, out var recipe))
        {
            StartCoroutine(CraftRoutine(recipe, inputAObject, inputBObject));
        }
        else
        {
            Debug.Log("No recipe found for: " + a?.objectName + " + " + b?.objectName);
        }
    }

    private ObjectData ResolveItemData(ObjectData data, GameObject obj)
    {
        if (data != null) return data;
        if (obj != null)
        {
            var ing = obj.GetComponentInParent<Ingredient>();
            if (ing != null) return ing.item;
        }
        return null;
    }

    private IEnumerator CraftRoutine(Recipe recipe, GameObject aObj, GameObject bObj)
    {
        Debug.Log("crafting started");
        isCrafting = true;

        // Hide/disable inputs during crafting if they are world objects
        if (aObj) aObj.SetActive(false);
        if (bObj) bObj.SetActive(false);

        yield return new WaitForSeconds(recipe.craftSeconds);

        // Consume inputs (destroy world objects if provided)
        if (aObj) Destroy(aObj);
        if (bObj) Destroy(bObj);
        inputAObject = inputBObject = null;
        inputAData = inputBData = null;

        // Spawn product
        if (recipe.resultItem != null && recipe.resultItem.equipPrefab != null)
        {
            Vector3 pos = outputPoint ? outputPoint.position : transform.position + transform.forward * 0.5f;
            Quaternion rot = outputPoint ? outputPoint.rotation : Quaternion.identity;

            var product = Instantiate(recipe.resultItem.equipPrefab, pos, rot);

            // make sure it can be used again as ingredient later if needed
            var ing = product.GetComponent<Ingredient>();
            if (ing == null) ing = product.AddComponent<Ingredient>();
            ing.item = recipe.resultItem;

            var rb = product.GetComponent<Rigidbody>();
            if (rb) { rb.isKinematic = false; rb.useGravity = true; }
        }

        isCrafting = false;
    }
}
