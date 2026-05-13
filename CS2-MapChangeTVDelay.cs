using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;
using System.Collections.Generic;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線10秒精確認人重啟";
    public override string ModuleVersion => "1.8.0";

    private readonly string _prefix = " \x06[ \x041 v 1 對 戰 模 式 \x06] \x01";
    
    // 用來儲存斷線那一刻，原本場上「所有」玩家的 SteamID
    private List<ulong> _originalMatchPlayers = new();

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

        // 【紀錄關鍵人物】
        // 找出目前還在場上的真人
        var currentActive = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3))
            .ToList();

        // 如果剩下不到 2 人，開始 10 秒認人倒數
        if (currentActive.Count < 2)
        {
            // 紀錄下這場比賽「目前剩下的這幾個人」的 SteamID
            _originalMatchPlayers = currentActive.Select(p => p.SteamID).ToList();

            Server.PrintToChatAll($"{_prefix} 偵測到玩家離開，比賽中斷。");
            Server.PrintToChatAll($"{_prefix} 等待原玩家重連，\x02 10 秒 \x01 後檢查身份...");
            
            AddTimer(10.0f, () => {
                // 取得 10 秒後，現在場上的真人
                var nowPlayers = Utilities.GetPlayers()
                    .Where(p => p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3))
                    .ToList();

                // 【精確判定邏輯】
                // 1. 人數必須回到 2 人
                // 2. 原本留下來的那個人 (_originalMatchPlayers) 必須還在場上
                bool isPeopleEnough = nowPlayers.Count >= 2;
                bool isOriginalStayedPlayerStillHere = _originalMatchPlayers.All(id => nowPlayers.Any(p => p.SteamID == id));

                if (isPeopleEnough && isOriginalStayedPlayerStillHere)
                {
                    Server.PrintToChatAll($"{_prefix} 原玩家已就位，比賽繼續。");
                }
                else
                {
                    // 如果原本的人走了，或者 10 秒後人還是不夠，直接重啟
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
