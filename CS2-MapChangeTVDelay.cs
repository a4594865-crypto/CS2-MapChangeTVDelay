using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 工作坊地圖重載工具";
    public override string ModuleVersion => "1.4.3";

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            HandlePlayerLeave();
            return HookResult.Continue;
        });

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
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null || gameRules.WarmupPeriod) return;

        var activePlayers = Utilities.GetPlayers().Where(p => 
            p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        if (activePlayers.Count < 2)
        {
            Server.PrintToChatAll(" \x02[1V1] 偵測到比賽中途有人離開...");
            Server.PrintToChatAll(" \x02[1V1] 5 秒後將重新載入工作坊地圖以重置狀態。");
            
            AddTimer(5.0f, () => {
                var finalCheck = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3));
                if (finalCheck < 2)
                {
                    // 針對工作坊地圖的重載邏輯
                    string mapName = Server.MapName;
                    
                    // 如果地圖名包含 workshop 字樣，通常需要確保路徑正確
                    // 在 CS2 中，最保險的重載當前地圖方式是直接對當前 MapName 執行 map 指令
                    // 或是使用 host_changelevel
                    Server.ExecuteCommand($"host_workshop_map {mapName}");
                }
            });
        }
    }
}
