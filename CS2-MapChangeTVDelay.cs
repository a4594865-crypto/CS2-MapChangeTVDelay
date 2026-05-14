using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using System.Linq;
using System; 

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 訊息提示與槍枝顯示";
    public override string ModuleVersion => "2.5.0"; 

    private readonly string _prefix = " [\x04 1 v 1 對 戰 模 式 \x01] ";
    private bool _isMatchEnded = false;
    private readonly System.Collections.Generic.HashSet<ulong> _disconnectingPlayers = new();

    // 判斷熱身模式：熱身期間不顯示提示訊息
    private bool IsInWarmup()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        return gameRules == null || gameRules.WarmupPeriod;
    }

    public override void Load(bool hotReload)
    {
        // [功能 1] 槍枝顯示指令
        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) => {
            _isMatchEnded = true;
            return HookResult.Continue;
        });

        // [功能 2] 處理玩家離線訊息
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) => {
            if (IsInWarmup() || @event.Userid == null || _isMatchEnded) 
                return HookResult.Continue;

            var player = @event.Userid;
            if (player.TeamNum <= 1) return HookResult.Continue;

            if (player.SteamID > 0) _disconnectingPlayers.Add(player.SteamID);

            AddTimer(1.5f, () => {
                HandlePlayerLeaveMsg(player.PlayerName, true);
                _disconnectingPlayers.Remove(player.SteamID);
            }); 
            return HookResult.Continue;
        });

        // [功能 3] 處理玩家換隊訊息 (跳觀戰)
        RegisterEventHandler<EventPlayerTeam>((@event, info) => {
            if (IsInWarmup() || @event.Userid == null || !@event.Userid.IsValid || _isMatchEnded) 
                return HookResult.Continue;

            var player = @event.Userid;

            // 檢查是否為最後一人
            int realPlayerCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0);
            if (realPlayerCount <= 1 || _disconnectingPlayers.Contains(player.SteamID)) 
                return HookResult.Continue;

            if (@event.Oldteam > 1 && @event.Team <= 1)
            {
                AddTimer(0.1f, () => {
                    if (_disconnectingPlayers.Contains(player.SteamID)) return;

                    int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));
                    if (activeCount < 2)
                    {
                        Server.PrintToChatAll($"{_prefix}玩 家 \x10{player.PlayerName}\x01 切 換 到 \x10 觀 戰 \x01 比 賽 已 中 止");
                        
                        AddTimer(3.0f, () => {
                            if (!_isMatchEnded)
                                Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10 !R \x01 重 新 對 戰 開 始");
                        });
                    }
                });
                AddTimer(1.2f, () => HandlePlayerLeaveMsg(player.PlayerName, false));
            }
            return HookResult.Continue;
        });
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        player.PrintToChat($" {ChatColors.Orange}可 在 聊 天 欄 位 輸 入 您 要 的 武 器...");
    }

    private void HandlePlayerLeaveMsg(string playerName, bool isDisconnect)
    {
        if (IsInWarmup() || _isMatchEnded) return;

        int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));

        if (activeCount < 2)
        {
            if (isDisconnect && activeCount == 1) 
            {
                // 有人斷線且對戰席只剩一人時廣播
                Server.PrintToChatAll($"{_prefix}玩 家 \x10{playerName}\x01 已 跳 出 \x10 離 線 \x01 比 賽 已 中 止");
                
                AddTimer(3.0f, () => {
                    if (!_isMatchEnded)
                        Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10 !R \x01 重 新 對 戰 開 始");
                });
            }
        }
    }
}
