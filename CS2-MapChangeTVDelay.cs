using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using System.Linq;
using System; 

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 訊息提示與 Log 監控";
    public override string ModuleVersion => "2.6.9"; 

    private readonly string _prefix = " [\x04 1 v 1 對 戰 模 式 \x01] ";
    private bool _isMatchEnded = false;
    private bool _isServerShuttingDown = false; 
    private readonly System.Collections.Generic.HashSet<ulong> _disconnectingPlayers = new();

    private bool IsInWarmup()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        return gameRules == null || gameRules.WarmupPeriod;
    }

    public override void Load(bool hotReload)
    {
        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        // [功能新增] 雙向聊天轉發 (選手<->觀戰者)
        RegisterEventHandler<EventPlayerChat>((@event, info) =>
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid) return HookResult.Continue;

            string message = @event.Text;
            var allPlayers = Utilities.GetPlayers();

            // 1. 如果是比賽中的選手發言 (T=2, CT=3) -> 轉發給所有觀戰者
            if (player.TeamNum == (byte)CsTeam.Terrorist || player.TeamNum == (byte)CsTeam.CounterTerrorist)
            {
                foreach (var p in allPlayers.Where(p => p.IsValid && p.TeamNum == (byte)CsTeam.Spectator))
                {
                    p.PrintToChat($" {ChatColors.Grey}[觀賽] {ChatColors.White}{player.PlayerName}: {message}");
                }
            }
            // 2. 如果是觀戰者發言 -> 強制廣播給所有人 (選手 + 其他觀戰者)
            else if (player.TeamNum == (byte)CsTeam.Spectator)
            {
                Server.PrintToChatAll($" {ChatColors.Red}[觀戰者] {ChatColors.White}{player.PlayerName}: {message}");
                return HookResult.Stop; // 阻止原訊息丟失
            }

            return HookResult.Continue;
        });

        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) => {
            _isMatchEnded = true;
            return HookResult.Continue;
        });

        // [功能 2] 處理玩家離線 Log 與訊息
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) => {
            if (IsInWarmup() || @event.Userid == null || _isMatchEnded || _isServerShuttingDown) 
                return HookResult.Continue;

            var player = @event.Userid;
            if (!player.IsValid || player.IsBot) return HookResult.Continue;

            if (player.TeamNum <= 1) return HookResult.Continue;

            string cachedPlayerName = player.PlayerName;
            ulong cachedSteamId = player.SteamID;

            if (cachedSteamId > 0) _disconnectingPlayers.Add(cachedSteamId);

            Console.WriteLine($"[1V1 Log] 偵測到比賽中玩家 {cachedPlayerName} ({cachedSteamId}) 正在中斷連線...");

            AddTimer(1.5f, () => {
                if (_isServerShuttingDown) return;
                HandlePlayerDisconnectMsg(cachedPlayerName);
                _disconnectingPlayers.Remove(cachedSteamId);
            }); 
            return HookResult.Continue;
        });

        // [功能 3] 處理玩家換隊 Log 與訊息
        RegisterEventHandler<EventPlayerTeam>((@event, info) => {
            if (IsInWarmup() || @event.Userid == null || !@event.Userid.IsValid || _isMatchEnded || _isServerShuttingDown) 
                return HookResult.Continue;

            var player = @event.Userid;
            if (player.IsBot) return HookResult.Continue;

            int realPlayerCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0);
            if (realPlayerCount <= 1 || _disconnectingPlayers.Contains(player.SteamID)) 
                return HookResult.Continue;

            if (@event.Oldteam > 1 && @event.Team <= 1)
            {
                string cachedName = player.PlayerName;
                ulong cachedId = player.SteamID;

                AddTimer(0.1f, () => {
                    if (_isServerShuttingDown || _disconnectingPlayers.Contains(cachedId)) return;

                    int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));
                    if (activeCount < 2)
                    {
                        Server.PrintToChatAll($"{_prefix}玩 家 \x10{cachedName}\x01 切 換 到 \x10 觀 戰 \x01 比 賽 已 中 止");
                        Console.WriteLine($"[1V1 Log] 玩家 {cachedName} 切換到觀戰，比賽已中止");
                        
                        AddTimer(3.0f, () => {
                            if (!_isMatchEnded && !_isServerShuttingDown)
                                Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10 !R \x01 重 新 對 戰 開 始");
                        });
                    }
                });
            }
            return HookResult.Continue;
        });
    }

    public override void Unload(bool hotReload)
    {
        _isServerShuttingDown = true;
        base.Unload(hotReload);
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        
        player.PrintToChat($" {ChatColors.Orange}可 在 聊 天 欄 位 輸 入 您 要 的 武器，以 下 是 常 用 武器");
        player.PrintToChat($" -----------------------------------------------------------------");
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ]  {ChatColors.Blue}!dg {ChatColors.White}[ 沙漠之鷹 ]    、 {ChatColors.Blue}!usp {ChatColors.White}[ USP-S ]    、 {ChatColors.Blue}!gk {ChatColors.White}[ 格洛克 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ]    、 {ChatColors.Orange}!awp {ChatColors.White}[ 狙擊步槍 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!ak {ChatColors.White}[ AK-47 ]    、 {ChatColors.Green}!a1 {ChatColors.White}[ M4-A1 ]    、 {ChatColors.Green}!a4 {ChatColors.White}[ M4-A4 ]");
    }

    private void HandlePlayerDisconnectMsg(string playerName)
    {
        if (IsInWarmup() || _isMatchEnded || _isServerShuttingDown) return;

        int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));

        if (activeCount == 1)
        {
            Server.PrintToChatAll($"{_prefix}玩 家 \x10{playerName}\x01 已 跳 出 \x10 離 線 \x01 比 賽 已 中 止");
            Console.WriteLine($"[1V1 Log] 玩家 {playerName} 斷線離場，比賽已中止");
            
            if (!_isMatchEnded && !_isServerShuttingDown)
            {
                Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10 !R \x01 重 新 對 戰 開 始");
            }
        }
    }
}
