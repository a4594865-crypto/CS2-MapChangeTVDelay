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
    public override string ModuleVersion => "2.2.2"; // 更新版本號

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
        AddTimer(1.0f, () => {
            if (_isServerShuttingDown) return;
            if (IsInWarmup()) return;

            var teams = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager");
            if (teams != null)
            {
                var tTeam = teams.FirstOrDefault(t => t.TeamNum == 2);
                var ctTeam = teams.FirstOrDefault(t => t.TeamNum == 3);

                if (tTeam != null && ctTeam != null)
                {
                    int tScore = tTeam.Score;
                    int ctScore = ctTeam.Score;

                    if (ctScore >= 30 || tScore >= 30)
                    {
                        return; 
                    }
                }
            }

            int activePlayers = Utilities.GetPlayers().Count(p => 
                p != null && 
                p.IsValid && 
                !p.IsBot && 
                p.SteamID > 0 && 
                (p.TeamNum == 2 || p.TeamNum == 3)
            );

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

        string formattedMessage = $"{senderPrefix} {nameColor}{playerName}{ChatColors.White}：{message}";

        var allPlayers = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot);
        foreach (var p in allPlayers)
        {
            p.PrintToChat(formattedMessage);
        }

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
