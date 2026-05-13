using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線/觀戰5秒絕對重啟(防呆強化版)";
    public override string ModuleVersion => "1.6.4";

    private readonly string _prefix = " [\x06 1 v 1 對 戰 模 式 \x06] \x01";
    
    private bool _isResetting = false;
    private bool _isMatchEnded = false; // 新增：用來標記比賽是否已結束

    public override void Load(bool hotReload)
    {
        // 1. 監控比賽結束 (關鍵：防止換圖期間觸發)
        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) =>
        {
            _isMatchEnded = true; // 比賽結束了，鎖定插件
            return HookResult.Continue;
        });

        // 2. 監控玩家斷線
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            string name = @event.Userid?.PlayerName ?? "未知玩家";
            AddTimer(0.2f, () => HandlePlayerLeave(name));
            return HookResult.Continue;
        });

        // 3. 監控玩家換隊
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Oldteam > 1) 
            {
                string name = @event.Userid?.PlayerName ?? "未知玩家";
                AddTimer(0.2f, () => HandlePlayerLeave(name));
            }
            return HookResult.Continue;
        });
    }

    private void HandlePlayerLeave(string playerName)
    {
        // 【防呆檢查】如果比賽已經結束（正在換圖中），直接跳過不動作
        if (_isMatchEnded) return;

        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null) return;

        // 如果在熱身，不執行
        if (gameRules.WarmupPeriod) return;

        // 如果已經在重啟倒數中，不重複執行
        if (_isResetting) return;

        var activePlayers = Utilities.GetPlayers().Where(p => 
            p != null && p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        if (activePlayers.Count < 2)
        {
            _isResetting = true;

            Server.PrintToChatAll($"{_prefix}玩家 \x04{playerName}\x01 離開 (\x04 斷 線 / 觀 戰 \x01)，比賽中止。");
            Server.PrintToChatAll($"{_prefix}伺服器將在 \x04 5 秒\x01 後「強制重載地圖」...");
            
            AddTimer(7.0f, () => {
                ExecuteForceReset();
            });
        }
    }

    private void ExecuteForceReset()
    {
        Server.ExecuteCommand("mp_backup_restore_load_autobackup 0");
        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");
        
        _isResetting = false;
        _isMatchEnded = false; // 重啟後恢復狀態
    }
}
