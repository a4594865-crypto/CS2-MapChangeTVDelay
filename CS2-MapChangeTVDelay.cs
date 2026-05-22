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

        // 監聽訊息，但不再使用 Handled 阻斷
        AddCommandListener("say", OnPlayerSay);
        AddCommandListener("say_team", OnPlayerSay);

        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) => {
            _isMatchEnded = true;
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) => {
            if (IsInWarmup() || @event.Userid == null || _isMatchEnded || _isServerShuttingDown) 
                return HookResult.Continue;

            var player = @event.Userid;
            if (!player.IsValid || player.IsBot) return HookResult.Continue;
            if (player.TeamNum <= 1) return HookResult.Continue;

            string cachedPlayerName = player.PlayerName;
            ulong cachedSteamId = player.SteamID;

            if (cachedSteamId > 0) _disconnectingPlayers.Add(cachedSteamId);

            AddTimer(1.5f, () => {
                if (_isServerShuttingDown) return;
                HandlePlayerDisconnectMsg(cachedPlayerName);
                _disconnectingPlayers.Remove(cachedSteamId);
            }); 
            return HookResult.Continue;
        });

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
                        Server.PrintToChatAll($"{_prefix}玩家 \x10{cachedName}\x01 切換到觀戰，比賽已中止");
                        
                        AddTimer(3.0f, () => {
                            // 增加遊戲狀態檢查，避免換圖過程中發送訊息
                            var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
                            if (gameRules != null && !gameRules.WarmupPeriod && !_isMatchEnded && !_isServerShuttingDown)
                                Server.PrintToChatAll($"{_prefix}請下一組玩家輸入 \x10 !R \x01 重新對戰開始");
                        });
                    }
                });
            }
            return HookResult.Continue;
        });
    }

    private HookResult OnPlayerSay(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return HookResult.Continue;
        string message = info.GetArg(1).Trim(); 
        if (string.IsNullOrWhiteSpace(message) || message.StartsWith("!") || message.StartsWith("/")) return HookResult.Continue;

        string playerName = player.PlayerName;
        string senderPrefix = $" {ChatColors.White}[所有人]{ChatColors.White}";
        string nameColor = (player.TeamNum == 1) ? $"{ChatColors.Grey}" : (player.TeamNum == 2 ? "\x10" : "\x0B");

        // 僅發送顯示訊息，不攔截系統指令
        Server.PrintToChatAll($"{senderPrefix} {nameColor}{playerName}{ChatColors.White}：{message}");

        // 改為 Continue，讓 MatchZy 等核心插件也能處理訊息
        return HookResult.Continue; 
    }

    public override void Unload(bool hotReload)
    {
        _isServerShuttingDown = true;
        base.Unload(hotReload);
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        player.PrintToChat($" {ChatColors.Orange}可在聊天欄位輸入指令，以下是常用武器");
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ] !dg, !usp, !gk | [ {ChatColors.Orange}狙擊{ChatColors.White} ] !ssg, !awp | [ {ChatColors.Green}步槍{ChatColors.White} ] !ak, !a1, !a4");
    }

    private void HandlePlayerDisconnectMsg(string playerName)
    {
        if (IsInWarmup() || _isMatchEnded || _isServerShuttingDown) return;
        int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));

        if (activeCount == 1)
        {
            Server.PrintToChatAll($"{_prefix}玩家 \x10{playerName}\x01 已跳出離線，比賽已中止");
            if (!_isMatchEnded && !_isServerShuttingDown)
            {
                Server.PrintToChatAll($"{_prefix}請下一組玩家輸入 \x10 !R \x01 重新對戰開始");
            }
        }
    }
}
