using System.Collections.Generic;
using UnityEngine;

// 连招链：数据驱动的"这一段之后能接什么"。
// 现在只有一个攻击键，NextOf 恒取候选 0（等价线性 1→2→3）；
// 阶段 7 加重攻击 / 气刃时，按输入挑选 candidateIndex 即可，签名不用改。
[CreateAssetMenu(fileName = "ComboChain_New", menuName = "ComboHit/ComboChain")]
public class ComboChain : ScriptableObject
{
    [System.Serializable]
    public class Node
    {
        public AttackData attack;
        public List<AttackData> next = new List<AttackData>();   // 允许接什么，按顺序
    }

    [SerializeField] private AttackData entry;                    // 起手招（Slash1）
    [SerializeField] private List<Node> nodes = new List<Node>();

    public AttackData Entry => entry;

    // current 这一招后面允许接的第 candidateIndex 个候选；找不到节点 / 没有候选 / 越界 ⇒ null（= 链到头）
    public AttackData NextOf(AttackData current, int candidateIndex = 0)
    {
        if (current == null) return null;

        Node node = FindNode(current);
        if (node == null || node.next == null) return null;
        if (candidateIndex < 0 || candidateIndex >= node.next.Count) return null;

        return node.next[candidateIndex];
    }

    private Node FindNode(AttackData attack)
    {
        if (attack == null || nodes == null) return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].attack == attack) return nodes[i];
        }
        return null;
    }

    // 链是资产：Unity 里拖错一格（空 entry / 死边 / 自环）必须当场报错，而不是等运行时
    private void OnValidate()
    {
        if (entry == null)
        {
            Debug.LogError($"[ComboChain] {name}：entry 为空", this);
            return;
        }
        if (FindNode(entry) == null)
        {
            Debug.LogError($"[ComboChain] {name}：entry（{entry.attackName}）不在节点表里", this);
        }

        if (nodes == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            Node node = nodes[i];
            if (node == null || node.attack == null)
            {
                Debug.LogError($"[ComboChain] {name}：nodes[{i}] 的 attack 为空", this);
                continue;
            }
            if (node.next == null) continue;

            for (int j = 0; j < node.next.Count; j++)
            {
                AttackData nxt = node.next[j];
                if (nxt == null)
                {
                    Debug.LogError($"[ComboChain] {name}：{node.attack.attackName}.next[{j}] 为空", this);
                    continue;
                }
                if (nxt == node.attack)
                {
                    Debug.LogError($"[ComboChain] {name}：{node.attack.attackName}.next[{j}] 是自环（会导致无限连）", this);
                }
                if (FindNode(nxt) == null)
                {
                    Debug.LogError($"[ComboChain] {name}：{node.attack.attackName}.next[{j}]（{nxt.attackName}）不在节点表里（死边）", this);
                }
            }
        }
    }
}
