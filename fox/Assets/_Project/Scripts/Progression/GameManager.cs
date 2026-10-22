using UnityEngine;

/// <summary>保留的游戏状态占位组件，目前尚未接入游戏流程。</summary>
public class GameManager : MonoBehaviour
{
    private enum State { MainMenu, Playing, Paused, GameOver }
}
