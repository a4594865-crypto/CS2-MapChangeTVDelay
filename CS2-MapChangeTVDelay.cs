using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線/觀戰5秒絕對重啟(強化判定版)";
    public override string ModuleVersion => "1.6.5";

    private readonly string _prefix = " [\x06 1 v 1 對 戰 模 式 \x06] \x01";
    
    private bool _isResetting = false;
    private bool _isMatchEnded = false;

    public override void Load(bool hotReload)
    {
        // 1. 監控比賽結束
        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) =>
        {
            _isMatchEnded = true;
            return HookResult.Continue;
        });

        // 2. 監控玩家斷線 (針對離開遊戲)
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            if (@event.Userid == null) return HookResult.Continue;
            
            string name = @event.Userid.PlayerName;
            // 斷線需要更長的延遲 (改為 1.0 秒)，確保伺服器已將該玩家移出名單
            AddTimer(1.0f, () => HandlePlayerLeave(name));
            return HookResult.Continue;
        });

        // 3. 監控玩家換隊 (針對跳觀戰)
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Oldteam > 1 && @event.Userid != null) 
            {
                string name = @event.Userid.PlayerName;
                // 跳觀戰維持較快反應 (0.2 秒)
                AddTimer(0.2f, () => HandlePlayerLeave(name));
            }
            return HookResult.Continue;
        });
    }

    private void HandlePlayerLeave(string playerName)
    {
        if (_isMatchEnded || _isResetting) return;

        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null || gameRules.WarmupPeriod) return;

        // 統計人數時，增加判定：必須是 IsValid (有效) 且 SteamID > 0 (代表完全連線中)
        var activePlayers = Utilities.GetPlayers().Where(p => 
            p != null && 
            p.IsValid && 
            !p.IsBot && 
            p.SteamID > 0 && 
            (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        // 如果剩下不到 2 人，執行重啟
        if (activePlayers.Count < 2)
        {
            _isResetting = true;

            Server.PrintToChatAll($"{_prefix}玩家 \x04{playerName}\x01 離開 (\x04 斷 線 / 觀 戰 \x01)，比賽中止。");
            Server.PrintToChatAll($"{_prefix}伺服器將在 \x04 5 秒\x01 後「強制重載地圖」...");
            
            AddTimer(5.0f, () => {
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
        _isMatchEnded = false;
    }
}
