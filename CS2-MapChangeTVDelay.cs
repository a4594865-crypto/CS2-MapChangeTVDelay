using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using System.Linq;
using System; 

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 智能提示與重啟控制";
    public override string ModuleVersion => "2.4.5"; 

    private readonly string _prefix = " [\x04 1 v 1 對 戰 模 式 \x01] ";
    private bool _isResetting = false;
    private bool _isMatchEnded = false;
    private readonly System.Collections.Generic.HashSet<ulong> _disconnectingPlayers = new();

    // --- 熱身判定：完全避免在熱身時執行重啟邏輯 ---
    private bool IsInWarmup()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        return gameRules == null || gameRules.WarmupPeriod;
    }

    public override void Load(bool hotReload)
    {
        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) => {
            _isMatchEnded = true;
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) => {
            // 熱身模式下直接跳過
            if (IsInWarmup() || @event.Userid == null || _isMatchEnded || _isResetting) 
                return HookResult.Continue;

            var player = @event.Userid;
            if (player.TeamNum <= 1) return HookResult.Continue;

            if (player.SteamID > 0) _disconnectingPlayers.Add(player.SteamID);

            AddTimer(1.5f, () => {
                HandlePlayerLeave(player.PlayerName, true);
                _disconnectingPlayers.Remove(player.SteamID);
            }); 
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) => {
            // 熱身模式下直接跳過
            if (IsInWarmup() || @event.Userid == null || !@event.Userid.IsValid || _isMatchEnded || _isResetting) 
                return HookResult.Continue;

            var player = @event.Userid;

            // 檢查真人數量，如果是最後一人離開且即將空城，則不處理換隊提示
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
                        // 恢復廣播訊息
                        Server.PrintToChatAll($"{_prefix}玩 家 \x10{player.PlayerName}\x01 切 換 到 \x10 觀 戰 \x01 比 賽 已 中 止");
                        Console.WriteLine($"[1V1 Log] 玩家 {player.PlayerName} 跳往觀戰，比賽中止。");
                        
                        AddTimer(3.0f, () => {
                            if (!_isResetting && !_isMatchEnded)
                                Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10 !R \x01 重 新 對 戰 開 始");
                        });
                    }
                });
                AddTimer(1.2f, () => HandlePlayerLeave(player.PlayerName, false));
            }
            return HookResult.Continue;
        });
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        player.PrintToChat($" {ChatColors.Orange}可 在 聊 天 欄 位 輸 入 您 要 的 武 器...");
    }

    private void HandlePlayerLeave(string playerName, bool isDisconnect)
    {
        // 再次確保不是在熱身期間觸發重啟
        if (IsInWarmup() || _isResetting || _isMatchEnded) return;

        int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));
        int totalHumanPlayers = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0);

        if (activeCount < 2)
        {
            if (totalHumanPlayers == 0) 
            {
                // 空城判定：包含觀戰席都沒人了
                Console.WriteLine($"[1V1 Log] 完全空城，執行重置。");
                _isResetting = true;
                AddTimer(5.0f, () => { ExecuteForceReset(); });
            }
            else if (isDisconnect && activeCount == 1) 
            {
                // 如果還有人在觀戰，則只噴離線訊息，不重啟
                Server.PrintToChatAll($"{_prefix}玩 家 \x10{playerName}\x01 已 跳 出 \x10 離 線 \x01 比 賽 已 中 止");
                Console.WriteLine($"[1V1 Log] 玩家 {playerName} 跳出斷線，比賽中止。");

                AddTimer(3.0f, () => {
                    if (!_isResetting && !_isMatchEnded)
                        Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10 !R \x01 重 新 對 戰 開 始");
                });
            }
        }
    }

    private void ExecuteForceReset()
    {
        // 如果執行瞬間剛好有人進來或是變熱身，做最後攔截
        if (IsInWarmup()) 
        {
            _isResetting = false;
            return;
        }

        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");
        _isResetting = false;
        _isMatchEnded = false;
        Console.WriteLine($"[1V1 Log] 執行地圖重換，伺服器已自動初始化。");
    }
}
