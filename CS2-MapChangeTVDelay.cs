using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 比賽中斷地圖重載";
    public override string ModuleVersion => "1.4.0";

    public override void Load(bool hotReload)
    {
        // 1. 監控玩家斷線
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            CheckAndReload();
            return HookResult.Continue;
        });

        // 2. 監控玩家換隊
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Oldteam > 1) 
            {
                CheckAndReload();
            }
            return HookResult.Continue;
        });
    }

    private void CheckAndReload()
    {
        // --- 核心判斷：是否在熱身中 ---
        // 如果現在是熱身階段，直接跳過，不執行重載
        if (GameRules().WarmupPeriod) 
        {
            return; 
        }

        // 如果不是熱身（代表比賽已經開始），檢查人數
        var activePlayers = Utilities.GetPlayers().Where(p => 
            p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        // 只要比賽中人數少於 2 人，觸發 5 秒重載
        if (activePlayers.Count < 2)
        {
            Server.PrintToChatAll(" \x02[1V1] 偵測到比賽中選手離開，5 秒後重新載入地圖...");
            
            AddTimer(5.0f, () => {
                // 再次執行地圖重新載入，徹底重置所有插件
                Server.ExecuteCommand($"map {Server.MapName}");
            });
        }
    }

    // 輔助函數：獲取遊戲規則
    private static CCSGameRules GameRules()
    {
        return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
    }
}
