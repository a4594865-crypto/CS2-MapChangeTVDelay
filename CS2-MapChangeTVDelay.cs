using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線5秒絕對重啟 (工作坊優化版)";
    public override string ModuleVersion => "1.5.0";

    public override void Load(bool hotReload)
    {
        // 1. 監控玩家斷線
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            HandlePlayerLeave();
            return HookResult.Continue;
        });

        // 2. 監控玩家換隊 (換到觀戰視同離開)
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Oldteam > 1) 
            {
                HandlePlayerLeave();
            }
            return HookResult.Continue;
        });
    }

    private void HandlePlayerLeave()
    {
        // 獲取遊戲規則
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;

        if (gameRules == null) return;

        // 【辨識狀態】如果是熱身模式，直接跳過（不影響準備階段）
        if (gameRules.WarmupPeriod)
        {
            return;
        }

        // 【人數檢查】統計 T 和 CT 的真人數量 (排除 Bot)
        var activePlayers = Utilities.GetPlayers().Where(p => 
            p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        // 如果比賽中人數少於 2 人，執行暴力重啟
        if (activePlayers.Count < 2)
        {
            Server.PrintToChatAll(" \x06[ \x041 v 1 對 戰 模 式 \x06] \x01 偵測到玩家離開，比賽中斷。");
            Server.PrintToChatAll(" \x06[ \x041 v 1 對 戰 模 式 \x06] \x01 玩家10秒內伺服器將在 5 秒後「重新載入地圖」。");
            
            AddTimer(5.0f, () => {
                // 再次確認人數，避免 5 秒內有人連回來
                var finalCheck = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3));
                if (finalCheck < 2)
                {
                    ExecuteForceReset();
                }
            });
        }
    }

    private void ExecuteForceReset()
    {
        // 1. 終止比賽自動恢復機制 (解決回合數/分數留著的問題)
        Server.ExecuteCommand("mp_backup_restore_load_autobackup 0");
        
        // 2. 使用工作坊專用指令強制重啟地圖
        // 這會導致所有插件 (含 SLAYER) 卸載並重新載入，徹底洗掉計時器
        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");

        // 3. 備份機制：確保載入後進入熱身模式
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");
    }
}
