using UnityEngine;

public class ThrowableKillOnHit : MonoBehaviour
{
    [Header("Only kills when thrown")]
    [SerializeField] private bool requireArmed = true;

    [Header("Monster identification")]
    [SerializeField] private string monsterLayerName = "Monster";

    private bool armed;
    private int monsterLayer = -1;

    private void Awake()
    {
        monsterLayer = LayerMask.NameToLayer(monsterLayerName);
        if (monsterLayer < 0)
            Debug.LogWarning($"[{name}] Layer '{monsterLayerName}' not found. Create it in Tags & Layers.");
    }

    public void Arm() => armed = true;
    public void Disarm() => armed = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (requireArmed && !armed) return;

        // First, make sure it hit something on the Monster layer
        if (monsterLayer >= 0 && collision.gameObject.layer != monsterLayer)
            return;

        // Then, find the actual MonsterAI in the parent chain
        var monster = collision.collider.GetComponentInParent<MonsterAI>();
        if (monster == null) return;

        // Destroy ONLY the monster GameObject (not the whole scene/world root)
        Destroy(monster.gameObject);

        // Destroy the cube too
        Destroy(gameObject);
    }
}
