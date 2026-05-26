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
    public override string ModuleVersion => "2.0.0"; // 修正編譯，大版本號升級

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

        // 監聽單局結束：判斷「時間到誰血多就贏」
        RegisterEventHandler<EventRoundEnd>((@event, info) => {
            if (_isServerShuttingDown || IsInWarmup()) return HookResult.Continue;

            // Reason 13 = TargetSaved (時間到，CT 守住)
            // Reason 9 = RoundDraw (平局/超時)
            if (@event.Reason == 13 || @event.Reason == 9)
            {
                HandleTimeoutHealthCheck();
            }

            return HookResult.Continue;
        });

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

    private void HandleTimeoutHealthCheck()
    {
        var ctPlayer = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.TeamNum == 3 && p.PawnIsAlive);
        var tPlayer = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.TeamNum == 2 && p.PawnIsAlive);

        if (ctPlayer == null || tPlayer == null) return;

        int ctHealth = ctPlayer.PlayerPawn.Value?.Health ?? 0;
        int tHealth = tPlayer.PlayerPawn.Value?.Health ?? 0;

        var gameRulesProxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
        var gameRules = gameRulesProxy?.GameRules;
        if (gameRules == null || gameRulesProxy == null) return;

        if (ctHealth > tHealth)
        {
            // CT 血多：保持原生結算
        }
        else if (tHealth > ctHealth)
        {
            // T 血多：手動把 T 隊的分數加 1 (陣列索引 2 代表 T隊)
            gameRules.MatchStats_RoundsTotal[2] += 1;
            
            // 💡 修正：傳入 gameRulesProxy (它是 CBaseEntity)，資訊才會正確同步到客戶端
            Utilities.SetStateChanged(gameRulesProxy, "CCSGameRulesProxy", "m_pGameRules"); 
        }
    }

    private void CheckAndResetGame()
    {
        AddTimer(1.0f, () => {
            if (_isServerShuttingDown) return;
            if (IsInWarmup()) return;

            var gameRulesProxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
            var gameRules = gameRulesProxy?.GameRules;
            if (gameRules != null)
            {
                // 💡 修正：從正確的 MatchStats 陣列中抓取兩隊得分 (索引 2 是 T，索引 3 是 CT)
                int tScore = gameRules.MatchStats_RoundsTotal[2];
                int ctScore = gameRules.MatchStats_RoundsTotal[3];

                if (ctScore >= 30 || tScore >= 30)
                {
                    return; 
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
                
                Console.WriteLine($"[1V1重置 Log] 比賽中途離場，重置凍結暖身。");
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
