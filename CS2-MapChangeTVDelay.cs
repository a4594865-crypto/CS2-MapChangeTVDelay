using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線5秒絕對重啟";
    public override string ModuleVersion => "1.2.0";

    public override void Load(bool hotReload)
    {
        // 1. 監控玩家斷線
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            // 只要有人斷開，立即觸發倒數邏輯
            TriggerCountdownRestart();
            return HookResult.Continue;
        });

        // 2. 監控玩家換隊 (換到觀戰也視同離開比賽)
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            // 如果是從 T 或 CT 換走，觸發倒數
            if (@event.Oldteam > 1) 
            {
                TriggerCountdownRestart();
            }
            return HookResult.Continue;
        });
    }

    private void TriggerCountdownRestart()
    {
        // 1. 立即廣播通知
        Server.PrintToChatAll(" \x02[1V1系統] 偵測到選手離開，比賽結束。");
        Server.PrintToChatAll(" \x02[1V1系統] 地圖將在 5 秒後重新啟動並恢復熱身...");

        // 2. 設定一個不可逆的 5 秒定時器
        AddTimer(5.0f, () => {
            ExecuteForceReset();
        });
    }

    private void ExecuteForceReset()
    {
        // 執行「暴力還原」指令：無視任何狀態，強制重開
        // mp_restartgame 1 會終止當前回合並重置所有實體
        Server.ExecuteCommand("mp_restartgame 1");
        
        // 確保重啟後直接進入熱身，且時間凍結
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");

        Server.PrintToChatAll(" \x05[1V1系統] 地圖已重啟，恢復熱身狀態，等待下一對玩家。");
    }
}
