using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;
using System.Collections.Generic;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線10秒精確認人重啟";
    public override string ModuleVersion => "1.8.1";

    private readonly string _prefix = " [\x06 1 v 1 對 戰 模 式 \x06] \x01";
    
    private List<ulong> _originalMatchPlayers = new();
    private bool _isCountingDown = false; // 加入鎖定機制，防止重複觸發倒數

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            HandleMatchDisruption();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Oldteam > 1) HandleMatchDisruption();
            return HookResult.Continue;
        });
    }

    private void HandleMatchDisruption()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null || gameRules.WarmupPeriod) return;

        // 如果已經在倒數中，就不要再開一個新的計時器
        if (_isCountingDown) return;

        var currentActive = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3))
            .ToList();

        if (currentActive.Count < 2)
        {
            _isCountingDown = true; // 鎖定
            _originalMatchPlayers = currentActive.Select(p => p.SteamID).ToList();

            Server.PrintToChatAll($"{_prefix} 偵測到玩家離開，比賽中斷。");
            Server.PrintToChatAll($"{_prefix} 等待原玩家重連，\x02 10 秒 \x01 後檢查身份...");
            
            // 這裡確保一定是 10.0f 秒
            AddTimer(10.0f, () => {
                _isCountingDown = false; // 倒數結束，解除鎖定

                var nowPlayers = Utilities.GetPlayers()
                    .Where(p => p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3))
                    .ToList();

                bool isPeopleEnough = nowPlayers.Count >= 2;
                bool isOriginalStayedPlayerStillHere = _originalMatchPlayers.All(id => nowPlayers.Any(p => p.SteamID == id));

                if (isPeopleEnough && isOriginalStayedPlayerStillHere)
                {
                    Server.PrintToChatAll($"{_prefix} 原玩家已就位，比賽繼續。");
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
        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");
    }
}
