using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線5秒絕對強制重啟";
    public override string ModuleVersion => "2.0.0";

    private readonly string _prefix = " \x01[\x06 1 v 1 對 戰 模 式 \x01] ";
    private bool _isResetting = false;

    public override void Load(bool hotReload)
    {
        // 1. 監控玩家斷線
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            // 延遲一點點確保人數統計正確
            AddTimer(0.2f, () => CheckAndReset());
            return HookResult.Continue;
        });

        // 2. 監控玩家換隊 (包含跳觀戰)
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            // 如果玩家是從 T(2) 或 CT(3) 換走 (無論換到哪)
            if (@event.Oldteam == 2 || @event.Oldteam == 3)
            {
                // 這裡必須用 Timer 延遲，因為 EventPlayerTeam 觸發時玩家還沒真正換過去
                AddTimer(0.2f, () => CheckAndReset());
            }
            return HookResult.Continue;
        });
    }

    private void CheckAndReset()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null) return;

        // 【重點】請檢查你的伺服器是否一直卡在熱身？若是熱身就不會執行重啟。
        if (gameRules.WarmupPeriod) return;

        if (_isResetting) return;

        // 重新統計對戰席上的真人
        var activePlayers = Utilities.GetPlayers().Where(p => 
            p != null && p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        // 只要對戰席的人少於 2 位 (有人跳觀戰或斷線)
        if (activePlayers.Count < 2)
        {
            _isResetting = true;

            Server.PrintToChatAll($"{_prefix} \x02偵測到選手離開位子，比賽中止。");
            Server.PrintToChatAll($"{_prefix} \x01伺服器將在 \x025 秒\x01 後強制重啟地圖...");

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
        // 重載地圖後 _isResetting 會隨插件重新 Load 回到 false
    }
}
