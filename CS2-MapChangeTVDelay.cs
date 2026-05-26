using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using System;
using System.Linq; // 引入 LINQ 用於超輕量人數篩選

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 武器提示與聊天顯示";
    public override string ModuleVersion => "1.3.5"; // 更新版本號

    private bool _isServerShuttingDown = false; 

    // 💡 檢查當前伺服器是否處於熱身階段
    private bool IsInWarmup()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        return gameRules == null || gameRules.WarmupPeriod;
    }

    public override void Load(bool hotReload)
    {
        _isServerShuttingDown = false;

        // 註冊武器提示指令
        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        // 監聽聊天室訊息
        AddCommandListener("say", OnPlayerSay);
        AddCommandListener("say_team", OnPlayerSay);

        // 監聽玩家斷線與換隊事件
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) => {
            CheckAndResetGame();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) => {
            CheckAndResetGame();
            return HookResult.Continue;
        });

        // 註冊官方換圖/關圖事件：保護傘
        RegisterEventHandler<EventMapShutdown>((@event, info) => {
            _isServerShuttingDown = true;
            return HookResult.Continue;
        });
    }

    private void CheckAndResetGame()
    {
        // 延遲 1.0 秒執行，確保遊戲引擎已更新完玩家的陣存狀態
        AddTimer(1.0f, () => {
            if (_isServerShuttingDown) return;

            // 💡 核心修正：如果當前已經是熱身階段(Warmup)，直接跳出，甚麼都不做！
            if (IsInWarmup()) return;

            // 精準統計目前在「T隊(2)」與「CT隊(3)」的真實玩家人數（排除Bot與觀戰）
            int activePlayers = Utilities.GetPlayers().Count(p => 
                p != null && 
                p.IsValid && 
                !p.IsBot && 
                p.SteamID > 0 && 
                (p.TeamNum == 2 || p.TeamNum == 3)
            );

            // 如果是在「正規比賽期間」且對戰人數少於 2 人
            if (activePlayers < 2)
            {
                // 先開啟暖身狀態
                Server.ExecuteCommand("mp_warmup_start");
                
                // 針對工作坊地圖雙重保險：強制重啟對局，清空殘留數據
                Server.ExecuteCommand("mp_restartgame 1");
                
                Console.WriteLine($"[1V1重置 Log] 正式比賽中玩家中途離場（剩餘 {activePlayers} 人），已強制重置遊戲為暖身。");
            }
        });
    }

    private HookResult OnPlayerSay(CCSPlayerController? player, CommandInfo info)
    {
        if (_isServerShuttingDown || player == null || !player.IsValid) return HookResult.Continue;

        string message = info.GetArg(1).Trim(); 
        string playerName = player.PlayerName;

        if (string.IsNullOrWhiteSpace(message)) return HookResult.Continue;

        if (message.StartsWith("!") || message.StartsWith("/")) return HookResult.Continue;

        string senderPrefix = $" {ChatColors.White}[所有人]{ChatColors.White}";
        string nameColor = $"{ChatColors.White}";

        if (player.TeamNum == 1) nameColor = $"{ChatColors.Grey}";       // 觀戰
        else if (player.TeamNum == 2) nameColor = $"\x10";               // T隊
        else if (player.TeamNum == 3) nameColor = $"\x0B";               // CT隊

        Server.PrintToChatAll($"{senderPrefix} {nameColor}{playerName}{ChatColors.White}：{message}");

        string teamLabel = player.TeamNum == 1 ? "Spec" : (player.TeamNum == 2 ? "TS" : "CT");
        Console.WriteLine($"[{teamLabel}]{playerName}：{message}");

        return HookResult.Handled;
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        
        player.PrintToChat($" {ChatColors.Orange}您 可 在 聊 天 欄 位 輸 入 您 要 的 武 器，以 下 是 常 用 武 器");
        player.PrintToChat($" ----------------------------------------------------------------------");
        player.PrintToChat($" [ {ChatColors.LightBlue}手槍{ChatColors.White} ]  {ChatColors.LightBlue}!dg {ChatColors.White}[ 沙鷹 ] 、{ChatColors.LightBlue}!usp {ChatColors.White}[ USP ] 、{ChatColors.LightBlue}!gk {ChatColors.White}[ 格洛克 ] 、{ChatColors.LightBlue}!r8 {ChatColors.White}[ R8 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ] 、{ChatColors.Orange}!awp {ChatColors.White}[ AWP狙擊步槍 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!gr {ChatColors.White}[ Galil ] 、{ChatColors.Green}!ak {ChatColors.White}[ AK47 ] 、{ChatColors.Green}!a1 {ChatColors.White}[ M4A1 ] 、{ChatColors.Green}!a4 {ChatColors.White}[ M4A4 ]");
    }

    public override void Unload(bool hotReload)
    {
        _isServerShuttingDown = true;
        base.Unload(hotReload);
    }
}
