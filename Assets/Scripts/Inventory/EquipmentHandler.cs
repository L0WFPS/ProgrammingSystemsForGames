using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using UnityEngine;

public class EquipmentHandler : MonoBehaviour
{
    [Header("Attach point")]
    [SerializeField] private Transform handSocket;

    [Header("Physics while held")]
    [SerializeField] private bool makeRigidbodiesKinematic = true;
    [SerializeField] private bool disableGravityWhileHeld = true;
    [SerializeField] private bool setCollidersToTrigger = true;
    [SerializeField] private string heldLayerName = "";   // optional (e.g. "HeldItem")

    [Header("Optional pose anchor")]
    [SerializeField] private string poseChildName = "GripPose";

    private GameObject currentInstance;

    // Snapshot for original layers (per child)
    private Transform[] _savedTransforms;
    private int[] _savedLayers;

    public bool HasHeld => currentInstance != null;
    public GameObject CurrentInstance => currentInstance;

    public void Equip(ObjectData item)
    {
        Unequip();
        if (item == null || item.equipPrefab == null || handSocket == null) return;

        GameObject prefab = item.equipPrefab;

        currentInstance = Instantiate(prefab, handSocket);
        var t = currentInstance.transform;

        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = prefab.transform.localScale; // keep prefab’s scale

        // Optional pose snap
        var pose = currentInstance.transform.Find(poseChildName);
        if (pose != null)
        {
            var hand = handSocket;

            var delta = hand.rotation * Quaternion.Inverse(pose.rotation);
            t.rotation = delta * t.rotation;

            var posOffset = hand.position - pose.position;
            t.position += posOffset;

            t.SetParent(handSocket, true);
        }

        PrepareForHolding(currentInstance);
    }

    public void Unequip()
    {
        if (currentInstance != null)
        {
            Destroy(currentInstance);
            currentInstance = null;
        }
        ClearLayerSnapshot();
    }

    private void PrepareForHolding(GameObject go)
    {
        // 1) Snapshot original layers for the whole hierarchy
        _savedTransforms = go.GetComponentsInChildren<Transform>(true);
        _savedLayers = new int[_savedTransforms.Length];
        for (int i = 0; i < _savedTransforms.Length; i++)
            _savedLayers[i] = _savedTransforms[i].gameObject.layer;

        // 2) Optionally move entire hierarchy to a “held” layer
        if (!string.IsNullOrEmpty(heldLayerName))
        {
            int heldLayer = LayerMask.NameToLayer(heldLayerName);
            if (heldLayer >= 0) SetLayerRecursive(go, heldLayer);
        }

        // 3) Disable physics while held
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            if (makeRigidbodiesKinematic) rb.isKinematic = true;
            if (disableGravityWhileHeld) rb.useGravity = false;
        }

        if (setCollidersToTrigger)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                col.isTrigger = true;
        }

        // 4) Final snap to hand
        go.transform.SetParent(handSocket, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        // leave localScale as is (from prefab)
    }

    private void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private void RestoreOriginalLayers(GameObject go)
    {
        if (_savedTransforms == null || _savedLayers == null) return;

        // Restore per child; skip any that were destroyed/pooled
        for (int i = 0; i < _savedTransforms.Length; i++)
        {
            var tr = _savedTransforms[i];
            if (tr != null) tr.gameObject.layer = _savedLayers[i];
        }
    }

    private void ClearLayerSnapshot()
    {
        _savedTransforms = null;
        _savedLayers = null;
    }

    /// <summary>
    /// Detaches the held object, restores physics & ORIGINAL layers, and throws it forward.
    /// </summary>
    public GameObject ThrowHeld(Transform origin, float force)
    {
        if (currentInstance == null || origin == null) return null;

        GameObject thrown = currentInstance;
        currentInstance = null;

        // Detach
        thrown.transform.SetParent(null, true);

        // Restore colliders
        foreach (var col in thrown.GetComponentsInChildren<Collider>(true))
            col.isTrigger = false;

        // Restore RBs (or add one) for throwing
        var rbs = thrown.GetComponentsInChildren<Rigidbody>(true);
        if (rbs != null && rbs.Length > 0)
        {
            foreach (var rb in rbs)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
        else
        {
            var rootRb = thrown.AddComponent<Rigidbody>();
            rootRb.isKinematic = false;
            rootRb.useGravity = true;
        }

        //  Restore the exact original layer of every child
        RestoreOriginalLayers(thrown);
        ClearLayerSnapshot();

        // Place and throw
        thrown.transform.position = origin.position;
        thrown.transform.rotation = origin.rotation;

        var targetRb = thrown.GetComponent<Rigidbody>();
        if (targetRb == null) targetRb = thrown.GetComponentInChildren<Rigidbody>();
        if (targetRb != null)
            targetRb.AddForce(origin.forward.normalized * force, ForceMode.VelocityChange);

        return thrown;
    }

}
