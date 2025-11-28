using UnityEngine;
using UnityEngine.UIElements;
using static CraftingTable;

public class CraftingTable : MonoBehaviour
{
    public class Slot
    {
        public Vector3 localCenter = new Vector3(0, 0.2f, 0);
        public Vector3 size = new Vector3(0.4f, 0.4f, 0.4f);

        [HideInInspector]  public bool occupied;
        [HideInInspector]  public bool occupiedPrev;
    }
    public CraftingProcess process;

    [SerializeField] public Transform slot1Pos;
    [SerializeField] public Transform slot2Pos;
    [SerializeField] public Transform resaultPos;

    public LayerMask layer;

    [SerializeField] public bool slot1Free = true;
    [SerializeField] public bool slot2Free = true;
    [SerializeField] public bool resaultFree = true;

    public Slot slot1 = new Slot();
    public Slot slot2 = new Slot();
    public Slot resault = new Slot();

    public GameObject slot1Obj;
    public GameObject slot2Obj;

    public bool unfreezeOnExit = false;


    void FixedUpdate()
    {
        UpdateSlot(ref slot1, slot1Pos, ref slot1Free, ref process.inputAObject);
        UpdateSlot(ref slot2,  slot2Pos, ref slot2Free, ref process.inputBObject);
        UpdateSlot(ref resault, resaultPos, ref resaultFree, ref process.outputObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsAffected(other.gameObject)) return;

        if (IsAffected(other.gameObject))
        {
            if (slot1Free)
            {
                other.gameObject.transform.rotation = Quaternion.identity;
                Rigidbody rb = other.attachedRigidbody;
                if (rb == null) return;
                other.gameObject.transform.position = slot1Pos.position;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                slot1Obj = other.gameObject;
                return;
            }
            if (slot2Free)
            {
                other.gameObject.transform.rotation = Quaternion.identity;
                Rigidbody rb = other.attachedRigidbody;
                if (rb == null) return;
                other.gameObject.transform.position = slot2Pos.position;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                slot2Obj = other.gameObject;
                return;
            }
            return;
        }
    }

    void UpdateSlot(ref Slot slot, Transform pos, ref bool slotFree, ref GameObject item)
    {
        // world-space box from local offset/size
        Vector3 centerWS = pos.position;
        Quaternion rotWS = transform.rotation;
        Vector3 halfSize = slot.size * 0.5f;

        bool inside = Physics.CheckBox(centerWS, halfSize, rotWS, layer,
                                       QueryTriggerInteraction.Ignore);

        slot.occupiedPrev = slot.occupied;
        slot.occupied = inside;

        // Enter: was empty, now occupied
        if (!slot.occupiedPrev && slot.occupied)
        {
            slotFree = false;
        }

        // Exit: was occupied, now empty
        if (slot.occupiedPrev && !slot.occupied)
        {
            if(item != null)
            {
                item = null;
            }
            slotFree = true;
        }
    }

    // (Optional) visualize in editor; delete if you truly want zero extras
    void OnDrawGizmosSelected()
    {
        DrawBoxGizmo(slot1, Color.red, slot1Pos);
        DrawBoxGizmo(slot2, Color.green, slot2Pos);
        DrawBoxGizmo(resault, Color.cyan, resaultPos);
    }
    void DrawBoxGizmo(Slot slot, Color col, Transform pos)
    {
        Vector3 centerWS = pos.position;
        Gizmos.color = col;
        Gizmos.matrix = Matrix4x4.TRS(centerWS, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, slot.size);
        Gizmos.matrix = Matrix4x4.identity;
    }

    private bool IsAffected(GameObject go)
    {
        return (layer.value & (1 << go.layer)) != 0;
    }

}
