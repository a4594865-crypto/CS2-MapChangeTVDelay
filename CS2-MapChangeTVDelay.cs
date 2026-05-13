using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線/觀戰8秒絕對重啟(精準判定版)";
    public override string ModuleVersion => "1.6.7";

    private readonly string _prefix = " [\x06 1 v 1 對 戰 模 式 \x01] ";
    
    private bool _isResetting = false;
    private bool _isMatchEnded = false;

    public override void Load(bool hotReload)
    {
        // 1. 監控比賽結束 (防呆鎖定)
        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) =>
        {
            _isMatchEnded = true;
            return HookResult.Continue;
        });

        // 2. 監控玩家斷線 (針對離開遊戲)
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            if (@event.Userid == null || _isMatchEnded || _isResetting) return HookResult.Continue;
            
            string name = @event.Userid.PlayerName;
            // 斷線延遲稍微拉長到 1.5 秒，確保判定 100% 準確
            AddTimer(1.5f, () => HandlePlayerLeave(name));
            return HookResult.Continue;
        });

        // 3. 監控玩家換隊 (針對跳觀戰)
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Oldteam > 1 && @event.Userid != null && !_isMatchEnded && !_isResetting) 
            {
                string name = @event.Userid.PlayerName;
                // 跳觀戰維持 0.2 秒快速反應
                AddTimer(0.2f, () => HandlePlayerLeave(name));
            }
            return HookResult.Continue;
        });
    }

    private void HandlePlayerLeave(string playerName)
    {
        // 再次檢查鎖定狀態，防止計時器回調時重疊
        if (_isMatchEnded || _isResetting) return;

        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null || gameRules.WarmupPeriod) return;

        // 統計場上真人人數
        var activePlayers = Utilities.GetPlayers().Where(p => 
            p != null && 
            p.IsValid && 
            !p.IsBot && 
            p.SteamID > 0 && 
            (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        // 只要剩下不到 2 人，立刻觸發
        if (activePlayers.Count < 2)
        {
            // 【重要】立即上鎖，阻擋後面所有的檢查
            _isResetting = true;

            Server.PrintToChatAll($"{_prefix}玩家 \x04{playerName}\x01 離開 (\x04 斷 線 / 觀 戰 \x01)，比賽中止。");
            Server.PrintToChatAll($"{_prefix}伺服器將在 \x04 5 秒\x01 後「強制重載地圖」...");
            
            // 精準倒數 8 秒
            AddTimer(6.0f, () => {
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
        
        // 地圖更換後，Load 會重新跑，這裡設回 false 確保萬無一失
        _isResetting = false;
        _isMatchEnded = false;
    }
}
