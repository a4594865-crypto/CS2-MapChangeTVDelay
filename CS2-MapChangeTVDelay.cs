using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;
using System.Collections.Generic;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線10秒精確認人重啟";
    public override string ModuleVersion => "1.8.2";

    private readonly string _prefix = " \x06[ \x041 v 1 對 戰 模 式 \x06] \x01";
    
    // 儲存斷線瞬間留在場上的人
    private List<ulong> _originalMatchPlayers = new();
    private bool _isCountingDown = false;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            // 斷線事件通常需要延遲一幀處理，確保人數統計正確
            AddTimer(0.1f, () => HandleMatchDisruption());
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            // 只有從 T(2) 或 CT(3) 換走的人才觸發
            if (@event.Oldteam > 1) 
            {
                HandleMatchDisruption();
            }
            return HookResult.Continue;
        });
    }

    private void HandleMatchDisruption()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null || gameRules.WarmupPeriod || _isCountingDown) return;

        // 找出目前還在場上（對戰位）的真人
        var currentActive = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && !p.IsBot && p.Connected == PlayerConnectedState.PlayerConnected && (p.TeamNum == 2 || p.TeamNum == 3))
            .ToList();

        // 如果剩下不到 2 人，開始 10 秒認人倒數
        if (currentActive.Count < 2)
        {
            _isCountingDown = true;
            
            // 紀錄當下還留在場上的人的 SteamID
            _originalMatchPlayers = currentActive.Select(p => p.SteamID).ToList();

            Server.PrintToChatAll($"{_prefix} 偵測到選手離開，比賽中斷。");
            Server.PrintToChatAll($"{_prefix} 等待原玩家重連，\x02 10 秒 \x01 後檢查身份...");
            
            AddTimer(10.0f, () => {
                // 倒數結束，重新取得當前對戰位的人
                var nowPlayers = Utilities.GetPlayers()
                    .Where(p => p != null && p.IsValid && !p.IsBot && p.Connected == PlayerConnectedState.PlayerConnected && (p.TeamNum == 2 || p.TeamNum == 3))
                    .ToList();

                // 判斷 1：人數是否回歸到 2 人 (或以上)
                bool isPeopleEnough = nowPlayers.Count >= 2;
                
                // 判斷 2：原本斷線時「留在場上的人」，現在是否還在場上
                // 如果 _originalMatchPlayers 為空（代表兩人都跑了），則此項直接視為 false
                bool isOriginalStayedPlayerStillHere = _originalMatchPlayers.Count > 0 && 
                    _originalMatchPlayers.All(id => nowPlayers.Any(p => p.SteamID == id));

                if (isPeopleEnough && isOriginalStayedPlayerStillHere)
                {
                    Server.PrintToChatAll($"{_prefix} 選手已就位，取消地圖重啟。");
                    _isCountingDown = false; // 解鎖，比賽繼續
                }
                else
                {
                    // 沒人回來或是原本的人也走了，執行暴力重啟
                    ExecuteForceReset();
                    // 注意：重啟地圖後 _isCountingDown 會隨插件 Load 自動重置為 false
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
