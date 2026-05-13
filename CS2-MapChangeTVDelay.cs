using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 狀態感應重載工具";
    public override string ModuleVersion => "1.4.2";

    public override void Load(bool hotReload)
    {
        // 監控斷線
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            HandlePlayerLeave();
            return HookResult.Continue;
        });

        // 監控換隊
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

        // 1. 辨識狀態：如果是熱身模式，直接結束邏輯
        if (gameRules.WarmupPeriod)
        {
            return;
        }

        // 2. 比賽中邏輯：統計 T 和 CT 的真人數量
        var activePlayers = Utilities.GetPlayers().Where(p => 
            p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        // 3. 判定重載：人數不足 2 人時觸發
        if (activePlayers.Count < 2)
        {
            Server.PrintToChatAll(" \x02[1V1] 偵測到比賽中途有人離開...");
            Server.PrintToChatAll(" \x02[1V1] 伺服器將在 5 秒後重新載入地圖以重置狀態。");
            
            AddTimer(5.0f, () => {
                // 重新檢查人數，若 5 秒後依然沒人補回則執行地圖重新載入
                var finalCheck = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3));
                if (finalCheck < 2)
                {
                    Server.ExecuteCommand($"host_workshop_map {Server.MapName}");
                }
            });
        }
    }
}
