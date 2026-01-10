using UnityEngine;

public class EquipmentHandler : MonoBehaviour
{
    [Header("Attach point")]
    [SerializeField] private Transform handSocket;

    [Header("Physics while held")]
    [SerializeField] private bool makeRigidbodiesKinematic = true;
    [SerializeField] private bool disableGravityWhileHeld = true;
    [SerializeField] private bool setCollidersToTrigger = true;

    private GameObject currentInstance;

    // Snapshot original layers for whole hierarchy
    private Transform[] _savedTransforms;
    private int[] _savedLayers;

    public bool HasHeld => currentInstance != null;
    public GameObject CurrentInstance => currentInstance;

    public void Equip(ObjectData item)
    {
        Unequip();
        if (item == null || item.equipPrefab == null || handSocket == null) return;

        currentInstance = Instantiate(item.equipPrefab, handSocket);
        currentInstance.transform.localPosition = Vector3.zero;
        currentInstance.transform.localRotation = Quaternion.identity;

        PrepareForHolding(currentInstance);

        // While held: make sure lethal cubes are NOT armed
        var kill = currentInstance.GetComponentInChildren<ThrowableKillOnHit>(true);
        if (kill != null) kill.Disarm();
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
        // Snapshot layers
        _savedTransforms = go.GetComponentsInChildren<Transform>(true);
        _savedLayers = new int[_savedTransforms.Length];
        for (int i = 0; i < _savedTransforms.Length; i++)
            _savedLayers[i] = _savedTransforms[i].gameObject.layer;

        // Disable physics while held
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

        go.transform.SetParent(handSocket, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
    }

    private void RestoreOriginalLayers(GameObject go)
    {
        if (_savedTransforms == null || _savedLayers == null) return;
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

    /// <summary>Drop held item gently (NO throw, NO arming).</summary>
    public GameObject DropHeld(Transform origin)
    {
        if (currentInstance == null || origin == null) return null;

        GameObject dropped = currentInstance;
        currentInstance = null;

        dropped.transform.SetParent(null, true);

        foreach (var col in dropped.GetComponentsInChildren<Collider>(true))
            col.isTrigger = false;

        var rbs = dropped.GetComponentsInChildren<Rigidbody>(true);
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
            var rootRb = dropped.AddComponent<Rigidbody>();
            rootRb.isKinematic = false;
            rootRb.useGravity = true;
        }

        RestoreOriginalLayers(dropped);
        ClearLayerSnapshot();

        // Place it at drop point
        dropped.transform.position = origin.position;
        dropped.transform.rotation = origin.rotation;

        // Make sure cubes aren't lethal when dropped
        var kill = dropped.GetComponentInChildren<ThrowableKillOnHit>(true);
        if (kill != null) kill.Disarm();

        return dropped;
    }

    /// <summary>Throw held item (arms lethal cube if present).</summary>
    public GameObject ThrowHeld(Transform origin, float force)
    {
        if (currentInstance == null || origin == null) return null;

        GameObject thrown = currentInstance;
        currentInstance = null;

        thrown.transform.SetParent(null, true);

        foreach (var col in thrown.GetComponentsInChildren<Collider>(true))
            col.isTrigger = false;

        var rbs = thrown.GetComponentsInChildren<Rigidbody>(true);
        if (rbs != null && rbs.Length > 0)
        {
            foreach (var rb in rbs)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }
        else
        {
            var rootRb = thrown.AddComponent<Rigidbody>();
            rootRb.isKinematic = false;
            rootRb.useGravity = true;
            rootRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rootRb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        RestoreOriginalLayers(thrown);
        ClearLayerSnapshot();

        thrown.transform.position = origin.position;
        thrown.transform.rotation = origin.rotation;

        // Arm ONLY if the prefab has the kill script (yellow cube)
        var kill = thrown.GetComponentInChildren<ThrowableKillOnHit>(true);
        if (kill != null) kill.Arm();

        var targetRb = thrown.GetComponent<Rigidbody>();
        if (targetRb == null) targetRb = thrown.GetComponentInChildren<Rigidbody>();
        if (targetRb != null)
            targetRb.AddForce(origin.forward * force, ForceMode.VelocityChange);

        return thrown;
    }
}
