using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 网络玩家控制：NGO 生成玩家后，区分本地/远端控制权并注册进 GameManager。
/// - 本地（IsOwner）：读输入、可战斗，通知 GameManager 开战
/// - 远端：不读输入、武器停火，仅作为同步幽灵存在（NetworkTransform Owner 模式同步位置）
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerNetworkBehaviour : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        var player = GetComponent<PlayerController>();
        if (player == null) return;

        // 本地/远端控制权隔离（PlayerController / Weapon / 索敌均以 IsLocallyControlled 为开关）
        player.IsLocallyControlled = IsOwner;

        if (IsOwner)
        {
            // 出生点错开，避免两人重叠（Host 左侧 / Client 右侧）
            float side = IsHost ? -2f : 2f;
            Vector3 pos = player.transform.position;
            pos.x = side;
            player.transform.position = pos;
            player.SpawnPosition = pos;

            GameManager.Instance?.OnLocalPlayerReady(player);
        }
    }

    public override void OnNetworkDespawn()
    {
        // 断线/网络销毁：回主菜单（仅本端玩家销毁时触发）
        if (IsOwner)
        {
            GameManager.Instance?.ReturnToMainMenu();
        }
    }
}
