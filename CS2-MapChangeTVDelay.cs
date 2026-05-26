using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using System;
using System.Linq; 

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 武器提示與聊天顯示";
    public override string ModuleVersion => "2.2.0"; // 升級版本號

    private bool _isServerShuttingDown = false; 

    private bool IsInWarmup()
    {
        var gameRulesProxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
        return gameRulesProxy?.GameRules?.WarmupPeriod ?? true;
    }

    public override void Load(bool hotReload)
    {
        _isServerShuttingDown = false;

        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);
        AddCommandListener("say", OnPlayerSay);
        AddCommandListener("say_team", OnPlayerSay);

        // 監聽玩家斷線與換隊
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) => {
            CheckAndResetGame();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) => {
            CheckAndResetGame();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventMapShutdown>((@event, info) => {
            _isServerShuttingDown = true;
            return HookResult.Continue;
        });
    }

    private void CheckAndResetGame()
    {
        // 延遲 1.0 秒，確保引擎與離線狀態更新
        AddTimer(1.0f, () => {
            if (_isServerShuttingDown) return;
            if (IsInWarmup()) return;

            // 核心改動：直接上網撈地圖上的隊伍實體 (CCSTeam)
            var teams = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager");
            if (teams != null)
            {
                // TeamNum 2 是 T 隊，TeamNum 3 是 CT 隊
                var tTeam = teams.FirstOrDefault(t => t.TeamNum == 2);
                var ctTeam = teams.FirstOrDefault(t => t.TeamNum == 3);

                if (tTeam != null && ctTeam != null)
                {
                    int tScore = tTeam.Score;
                    int ctScore = ctTeam.Score;

                    // 如果有人到了 30 勝，這是正常完賽，直接跳出不重置！
                    if (ctScore >= 30 || tScore >= 30)
                    {
                        return; 
                    }
                }
            }

            // 統計目前在 T(2) 與 CT(3) 的真實對戰玩家人數
            int activePlayers = Utilities.GetPlayers().Count(p => 
                p != null && 
                p.IsValid && 
                !p.IsBot && 
                p.SteamID > 0 && 
                (p.TeamNum == 2 || p.TeamNum == 3)
            );

            // 正式比賽未完結且打球人數少於 2 人，判定為中途離場
            if (activePlayers < 2)
            {
                Server.ExecuteCommand("mp_warmup_start");
                Server.ExecuteCommand("mp_warmup_pausetimer 1");
                
                Console.WriteLine($"[1V1重置] 中途離場，重置暖身。");
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

        if (player.TeamNum == 1) nameColor = $"{ChatColors.Grey}";       
        else if (player.TeamNum == 2) nameColor = $"\x10";               
        else if (player.TeamNum == 3) nameColor = $"\x0B";               

        Server.PrintToChatAll($"{senderPrefix} {nameColor}{playerName}{ChatColors.White}：{message}");
        string teamLabel = player.TeamNum == 1 ? "Spec" : (player.TeamNum == 2 ? "TS" : "CT");
        Console.WriteLine($"[{teamLabel}]{playerName}：{message}");
        return HookResult.Handled;
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        player.PrintToChat($" {ChatColors.Orange}您 可 在 聊 天 欄 位 輸 入 您 要 的 武器，以 下 是 常 用 武器");
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
