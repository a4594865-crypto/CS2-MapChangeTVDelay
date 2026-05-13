using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線5秒地圖重新載入";
    public override string ModuleVersion => "1.3.0";

    public override void Load(bool hotReload)
    {
        // 1. 監控玩家斷線
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            TriggerMapReload();
            return HookResult.Continue;
        });

        // 2. 監控玩家換隊
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Oldteam > 1) 
            {
                TriggerMapReload();
            }
            return HookResult.Continue;
        });
    }

    private void TriggerMapReload()
    {
        Server.PrintToChatAll(" \x02[1V1] 偵測到選手離開，比賽結束。");
        Server.PrintToChatAll(" \x02[1V1] 伺服器將在 5 秒後重新載入地圖以重置所有插件...");

        // 設定 5 秒後執行重新載入
        AddTimer(5.0f, () => {
            // 獲取當前地圖名稱
            string currentMap = Server.MapName;
            
            // 執行 map 指令強制重新載入當前地圖
            // 這會讓所有插件（包括會踢人的那個）徹底重置
            Server.ExecuteCommand($"map {currentMap}");
        });
    }
}
