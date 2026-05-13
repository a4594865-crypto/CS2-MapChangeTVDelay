using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;
using System.Collections.Generic;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線10秒精確認人重啟";
    public override string ModuleVersion => "1.8.3";

    private readonly string _prefix = " \x06[ \x041 v 1 對 戰 模 式 \x06] \x01";
    
    private List<ulong> _originalMatchPlayers = new();
    private bool _isCountingDown = false;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            // 延遲處理確保斷線狀態已更新
            AddTimer(0.2f, () => HandleMatchDisruption());
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Oldteam > 1) 
            {
                HandleMatchDisruption();
            }
            return HookResult.Continue;
        });
    }

    private void HandleMatchDisruption()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null || gameRules.WarmupPeriod || _isCountingDown) return;

        // 【修正點】改用更穩定的玩家連線判斷方式
        var currentActive = Utilities.GetPlayers()
            .Where(p => p != null && 
                        p.IsValid && 
                        !p.IsBot && 
                        p.Connected == PlayerConnectedState.All && // 修正編譯錯誤：使用 .All
                        (p.TeamNum == 2 || p.TeamNum == 3))
            .ToList();

        if (currentActive.Count < 2)
        {
            _isCountingDown = true;
            _originalMatchPlayers = currentActive.Select(p => p.SteamID).ToList();

            Server.PrintToChatAll($"{_prefix} 偵測到選手離開，比賽中斷。");
            Server.PrintToChatAll($"{_prefix} 等待原玩家重連，\x02 10 秒 \x01 後檢查身份...");
            
            AddTimer(10.0f, () => {
                var nowPlayers = Utilities.GetPlayers()
                    .Where(p => p != null && 
                                p.IsValid && 
                                !p.IsBot && 
                                p.Connected == PlayerConnectedState.All && // 修正編譯錯誤：使用 .All
                                (p.TeamNum == 2 || p.TeamNum == 3))
                    .ToList();

                bool isPeopleEnough = nowPlayers.Count >= 2;
                
                // 檢查原本留下來的那個人是否還在場上
                bool isOriginalStayedPlayerStillHere = _originalMatchPlayers.Count > 0 && 
                    _originalMatchPlayers.All(id => nowPlayers.Any(p => p.SteamID == id));

                if (isPeopleEnough && isOriginalStayedPlayerStillHere)
                {
                    Server.PrintToChatAll($"{_prefix} 原玩家已就位，取消地圖重啟。");
                    _isCountingDown = false; 
                }
                else
                {
                    ExecuteForceReset();
                }
            });
        }
    }

    private void ExecuteForceReset()
    {
        Server.ExecuteCommand("mp_backup_restore_load_autobackup 0");
        // 確保重啟前解除鎖定，避免重啟後因邏輯錯誤卡死
        _isCountingDown = false;
        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");
    }
}
