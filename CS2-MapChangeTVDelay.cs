using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;
using System.Collections.Generic;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線10秒精確認人重啟";
    public override string ModuleVersion => "1.8.5";

    private readonly string _prefix = " \x06[ \x041 v 1 對 戰 模 式 \x06] \x01";
    
    // 改用字串儲存 SteamID 增加比對穩定性
    private List<string> _originalMatchPlayers = new();
    private bool _isCountingDown = false;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            AddTimer(0.5f, () => HandleMatchDisruption());
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            // 如果玩家換到 CT/T，且正在倒數中，我們手動觸發一次檢查
            if ((@event.Team == 2 || @event.Team == 3) && _isCountingDown)
            {
                // 這裡不執行 HandleMatchDisruption，交給 Timer 處理即可
            }
            
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

        // 統計當前在位子上的真人
        var currentActive = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3))
            .ToList();

        if (currentActive.Count < 2)
        {
            _isCountingDown = true;
            // 儲存為字串清單
            _originalMatchPlayers = currentActive.Select(p => p.SteamID.ToString()).ToList();

            Server.PrintToChatAll($"{_prefix} 偵測到選手離開，比賽中斷。");
            Server.PrintToChatAll($"{_prefix} 等待原玩家重連，\x02 10 秒 \x01 後檢查身份...");
            
            AddTimer(10.0f, () => {
                // 倒數結束，獲取現在對戰位的人
                var nowPlayers = Utilities.GetPlayers()
                    .Where(p => p != null && p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3))
                    .ToList();

                bool isPeopleEnough = nowPlayers.Count >= 2;
                
                // 比對 SteamID 字串
                bool isOriginalStayedPlayerStillHere = _originalMatchPlayers.Count > 0 && 
                    _originalMatchPlayers.All(id => nowPlayers.Any(p => p.SteamID.ToString() == id));

                // 【核心修正】如果原玩家回來了，這兩個條件必須同時成立
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
        _isCountingDown = false;
        Server.ExecuteCommand("mp_backup_restore_load_autobackup 0");
        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");
    }
}
