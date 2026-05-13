using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線5秒絕對強制重啟";
    public override string ModuleVersion => "2.1.0";

    private readonly string _prefix = " [\x06 1 v 1 對 戰 模 式 \x06] \x01";
    private bool _isResetting = false;

    public override void Load(bool hotReload)
    {
        // 1. 監控玩家斷線 (事件 A)
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            TriggerResetCheck();
            return HookResult.Continue;
        });

        // 2. 監控玩家離開 (更底層的 Hook)
        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            TriggerResetCheck();
        });

        // 3. 監控玩家換隊 (包含跳觀戰)
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            // 如果玩家是從 T(2) 或 CT(3) 換走
            if (@event.Oldteam == 2 || @event.Oldteam == 3)
            {
                TriggerResetCheck();
            }
            return HookResult.Continue;
        });
    }

    private void TriggerResetCheck()
    {
        // 延遲 0.5 秒再檢查，確保伺服器已經把離開的人踢出列表，統計才會精確
        AddTimer(0.5f, () => {
            CheckAndReset();
        });
    }

    private void CheckAndReset()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null) return;

        // 如果你希望「離開遊戲」一定要重啟，建議保留此判斷以防載入死循環
        // 但如果你的伺服器熱身結束得很快，可以考慮註解掉這行
        if (gameRules.WarmupPeriod) return;

        if (_isResetting) return;

        // 重新統計對戰席上的真人
        // 這次加入 Connected 狀態檢查，確保斷線的人不被算入
        var activePlayers = Utilities.GetPlayers().Where(p => 
            p != null && 
            p.IsValid && 
            !p.IsBot && 
            p.TeamNum > 1 && // 必須在 T 或 CT
            p.Connected == PlayerConnectedState.All // 必須是完全連線狀態
        ).ToList();

        // 只要對戰席的人少於 2 位 (有人跳觀戰或離開遊戲)
        if (activePlayers.Count < 2)
        {
            _isResetting = true;

            Server.PrintToChatAll($"{_prefix} \x02偵測到選手離開 (斷線/觀戰)，比賽中止。");
            Server.PrintToChatAll($"{_prefix} \x01伺服器將在\x02 5 秒\x01 後強制重啟地圖...");

            AddTimer(7.0f, () => {
                ExecuteForceReset();
            });
        }
    }

    private void ExecuteForceReset()
    {
        // 徹底重置指令
        Server.ExecuteCommand("mp_backup_restore_load_autobackup 0");
        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");
    }
}
