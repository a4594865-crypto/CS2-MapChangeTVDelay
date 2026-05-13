using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線/觀戰5秒絕對重啟";
    public override string ModuleVersion => "1.6.3";

    // 定義顏色前綴
    private readonly string _prefix = " [\x06 1 v 1 對 戰 模 式 \x06] \x01";
    
    // 防止 5 秒內多次觸發重啟邏輯
    private bool _isResetting = false;

    public override void Load(bool hotReload)
    {
        // 1. 監控玩家斷線
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            // 抓取斷線玩家名字
            string name = @event.Userid?.PlayerName ?? "未知玩家";
            // 延遲一點點，確保統計時玩家已離開
            AddTimer(0.2f, () => HandlePlayerLeave(name));
            return HookResult.Continue;
        });

        // 2. 監控玩家換隊 (跳觀戰也包含在此)
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            // 如果玩家原本在 T(2) 或 CT(3)
            if (@event.Oldteam > 1) 
            {
                // 抓取換隊玩家名字
                string name = @event.Userid?.PlayerName ?? "未知玩家";
                // 跳觀戰那一秒 TeamNum 尚未更新，必須延遲 0.2s 檢查人數才準確
                AddTimer(0.2f, () => HandlePlayerLeave(name));
            }
            return HookResult.Continue;
        });
    }

    private void HandlePlayerLeave(string playerName)
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null) return;

        // 如果在熱身，不執行重載
        if (gameRules.WarmupPeriod) return;

        // 如果已經在倒數中，就不重複執行
        if (_isResetting) return;

        // 統計目前在對戰位 (T/CT) 的真人數量
        var activePlayers = Utilities.GetPlayers().Where(p => 
            p != null && p.IsValid && !p.IsBot && (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        // 只要對戰席人數不足 2 (有人斷線或跳觀戰了)
        if (activePlayers.Count < 2)
        {
            _isResetting = true;

            // 標色說明：\x04 是亮綠色(人名)，\x02 是紅色(斷線/觀戰)，\x01 是白色(恢復)
            Server.PrintToChatAll($"{_prefix}玩家 \x04{playerName}\x01 離開 (\x02斷線/觀戰\x01)，比賽中止。");
            Server.PrintToChatAll($"{_prefix}伺服器將在 \x02 5 秒\x01 後「強制重載地圖」...");
            
            // 這裡改成 5.0f 以對應文字顯示
            AddTimer(7.0f, () => {
                ExecuteForceReset();
            });
        }
    }

    private void ExecuteForceReset()
    {
        // 清除備份，防止回合分數留存
        Server.ExecuteCommand("mp_backup_restore_load_autobackup 0");
        
        // 強制重載當前工作坊地圖，重置所有插件 (含 SLAYER)
        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");

        // 確保載入後為熱身狀態
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");
        
        // 重載地圖後 _isResetting 會重置
        _isResetting = false;
    }
}
