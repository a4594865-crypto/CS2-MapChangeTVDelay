using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Cvars;
using System;
using System.Linq; 

namespace OneVOneReset;

[MinimumApiVersion(369)]
public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 武器提示與中途離場重置";
    public override string ModuleVersion => "2.2.9";
    private bool _isServerShuttingDown = false; 
    
    // 【新增】用來記錄上一次印出訊息的時間，防止 CS2 引擎重複觸發
    private DateTime _lastAnnounceTime = DateTime.MinValue; 

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

        // 【修正】加入時間判斷，如果距離上次觸發不到 2 秒就忽略
        RegisterEventHandler<EventRoundAnnounceMatchStart>((@event, info) => {
            if ((DateTime.Now - _lastAnnounceTime).TotalSeconds > 5)
            {
                Console.WriteLine("[1V1單挑] 比 賽 開 始");
                _lastAnnounceTime = DateTime.Now;
            }
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) => {
            CheckAndResetGameImmediate();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) => {
            CheckAndResetGameImmediate();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventMapShutdown>((@event, info) => {
            _isServerShuttingDown = true;
            return HookResult.Continue;
        });
    }

    private void CheckAndResetGameImmediate()
    {
        Server.NextFrame(() => {
            if (_isServerShuttingDown) return;
            if (IsInWarmup()) return;

            var teams = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager");
            if (teams is not null)
            {
                var tTeam = teams.FirstOrDefault(t => t.TeamNum == 2);
                var ctTeam = teams.FirstOrDefault(t => t.TeamNum == 3);

                if (tTeam is not null && ctTeam is not null)
                {
                    if (ctTeam.Score >= 30 || tTeam.Score >= 30) return; 
                }
            }

            int activePlayers = Utilities.GetPlayers().Count(p => 
                p is not null && 
                p.IsValid && 
                !p.IsBot && 
                (p.TeamNum == 2 || p.TeamNum == 3) 
            );

            if (activePlayers < 2)
            {
                Server.ExecuteCommand("mp_warmup_start");

                var pauseConVar = ConVar.Find("mp_warmup_pausetimer");
                if (pauseConVar != null)
                {
                    pauseConVar.SetValue(1);
                }
                else
                {
                    Server.ExecuteCommand("mp_warmup_pausetimer 1");
                }

                Console.WriteLine($"[1V1重置] 中途離場，重置暖身。");
            }
        });
    }

    private HookResult OnPlayerSay(CCSPlayerController? player, CommandInfo info)
    {
        if (_isServerShuttingDown || player is null || !player.IsValid) 
            return HookResult.Continue;

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

        var allPlayers = Utilities.GetPlayers().Where(p => p is not null && p.IsValid && !p.IsBot);
        foreach (var p in allPlayers)
        {
            p.PrintToChat(formattedMessage);
        }

        return HookResult.Handled;
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player is null || !player.IsValid) return;
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
