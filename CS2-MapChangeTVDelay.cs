using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線5秒絕對強制重啟";
    public override string ModuleVersion => "1.9.1";

    // 顏色定義：\x06 綠色, \x01 預設白色
    private readonly string _prefix = " \x01[\x06 1 v 1 對 戰 模 式 \x01] ";
    
    // 用來確保 5 秒內只會觸發一次重啟邏輯
    private bool _isResetting = false;

    public override void Load(bool hotReload)
    {
        // 1. 玩家斷線
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            StartResetSequence();
            return HookResult.Continue;
        });

        // 2. 玩家換隊 (包含換到觀戰)
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Oldteam > 1) // 從 T(2) 或 CT(3) 離開
            {
                StartResetSequence();
            }
            return HookResult.Continue;
        });
    }

    private void StartResetSequence()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null) return;

        // 如果在熱身，不重啟 (讓玩家可以自由換隊或進出)
        if (gameRules.WarmupPeriod) return;

        // 如果已經在倒數了，就直接跳過，不重複發送訊息
        if (_isResetting) return;

        // 檢查真人人數
        var activePlayers = Utilities.GetPlayers().Where(p => 
            p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        // 只要對戰席剩不到 2 人
        if (activePlayers.Count < 2)
        {
            _isResetting = true;

            Server.PrintToChatAll($"{_prefix} \x02偵測到選手離開，比賽中止。");
            Server.PrintToChatAll($"{_prefix} \x01伺服器將在 \x025 秒\x01 後強制重啟地圖...");

            // 絕對重啟：5秒時間到就執行，不認人，不檢查
            AddTimer(5.0f, () => {
                ExecuteForceReset();
            });
        }
    }

    private void ExecuteForceReset()
    {
        // 1. 徹底關閉比賽自動恢復 (讓回合數歸零)
        Server.ExecuteCommand("mp_backup_restore_load_autobackup 0");
        
        // 2. 強制重新載入當前工作坊地圖 (這會強制 SLAYER 插件重新 Load)
        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");

        // 3. 確保重啟後進入熱身狀態
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");

        // 提示：重啟地圖後，插件會重新載入，_isResetting 會自動回到 false
    }
}
