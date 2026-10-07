using System;
using UnityEngine;

[DefaultExecutionOrder(1000)]   // 在所有默认脚本的 Update 之后执行：先采样输入，再统一推进逻辑帧
public class CombatClock : MonoBehaviour
{
    public const float TickRate = 60f;
    public const float TickDelta = 1f / TickRate;

    // 一帧最多补 5 个逻辑帧（约 83ms 追赶量）；触顶即丢弃积压时间，防止 spiral of death
    private const int MaxTicksPerFrame = 5;

    public static event Action<int> OnTick;
    public static int Frame { get; private set; }

    private float accumulator;

    // 本工程禁用了 Domain Reload：静态字段/事件不会在每次进 Play 时自动重置，必须手动清
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnTick = null;
        Frame = 0;
    }

    private void Update()
    {
        accumulator += Time.deltaTime;

        int ticks = 0;
        while (accumulator >= TickDelta && ticks < MaxTicksPerFrame)
        {
            accumulator -= TickDelta;
            Frame++;
            OnTick?.Invoke(Frame);
            ticks++;
        }

        if (ticks >= MaxTicksPerFrame) accumulator = 0f;
    }
}
