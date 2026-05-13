using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線強制恢復熱身";
    public override string ModuleVersion => "1.0.0";

    public override void Load(bool hotReload)
    {
        // 監控玩家斷線（離開伺服器）
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            // 延遲 1 秒執行，確保系統已經把離開的人移出名單
            AddTimer(1.0f, CheckPlayersAndReset);
            return HookResult.Continue;
        });

        // 監控玩家換隊（有人換到觀戰位也視同離開比賽）
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            AddTimer(1.0f, CheckPlayersAndReset);
            return HookResult.Continue;
        });
    }

    private void CheckPlayersAndReset()
    {
        // 1. 抓取目前在 T(2) 或 CT(3) 的真人玩家（排除 Bot）
        var activePlayers = Utilities.GetPlayers().Where(p => 
            p.IsValid && 
            !p.IsBot && 
            (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        // 2. 如果場上能打的人少於 2 個
        if (activePlayers.Count < 2)
        {
            // 3. 執行「暴力還原」指令
            // mp_restartgame 1：強制終止目前卡住的回合（這是解決你卡回合問題的藥方）
            Server.ExecuteCommand("mp_restartgame 1");
            
            // mp_warmup_start：立刻開啟熱身模式
            Server.ExecuteCommand("mp_warmup_start");
            
            // mp_warmup_pausetimer 1：讓熱身時間無限長，不要自動開始
            Server.ExecuteCommand("mp_warmup_pausetimer 1");

            // 在聊天室提醒剩下的那個人
            Server.PrintToChatAll(" \x02[1V1] 偵測到選手不足，已自動終止比賽並恢復熱身狀態。");
        }
    }
}
