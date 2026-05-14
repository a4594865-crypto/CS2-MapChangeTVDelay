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
    public override string ModuleVersion => "2.2.6"; 

    private readonly string _prefix = " [\x04 1 v 1 對 戰 模 式 \x01] ";
    private bool _isResetting = false;
    private bool _isMatchEnded = false;
    private readonly System.Collections.Generic.HashSet<ulong> _disconnectingPlayers = new();

    // --- 修正後的熱身判定：直接尋找 cs_gamerules 實體 ---
    private bool IsInWarmup()
    {
        // 直接找 cs_gamerules，這是 CS2 存放遊戲狀態的核心實體
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CGameRules>("cs_gamerules").FirstOrDefault();
        
        // 如果找不到規則實體，保守起見視為熱身模式（不啟動插件）
        if (gameRules == null) return true;
        
        // 直接讀取 WarmupPeriod 屬性
        return gameRules.WarmupPeriod;
    }

    public override void Load(bool hotReload)
    {
        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) => {
            _isMatchEnded = true;
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) => {
            // 熱身模式下，直接忽略事件
            if (IsInWarmup() || @event.Userid == null || _isMatchEnded || _isResetting) 
                return HookResult.Continue;
            
            var player = @event.Userid;
            string playerName = player.PlayerName ?? "玩家";
            ulong steamId = player.SteamID;

            if (player.TeamNum <= 1) return HookResult.Continue;
            if (steamId > 0) _disconnectingPlayers.Add(steamId);

            AddTimer(1.5f, () => {
                HandlePlayerLeave(playerName, true, steamId);
                _disconnectingPlayers.Remove(steamId);
            }); 
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) => {
            // 熱身模式下，換隊邏輯不啟動
            if (IsInWarmup() || @event.Userid == null || !@event.Userid.IsValid || _isMatchEnded || _isResetting) 
                return HookResult.Continue;

            var player = @event.Userid;
            string playerName = player.PlayerName ?? "玩家";
            ulong steamId = player.SteamID;

            int realPlayerCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0);
            if (realPlayerCount <= 1 || _disconnectingPlayers.Contains(steamId)) 
                return HookResult.Continue;

            if (@event.Oldteam > 1 && @event.Team <= 1)
            {
                AddTimer(0.1f, () => {
                    if (_disconnectingPlayers.Contains(steamId)) return;

                    int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));
                    if (activeCount < 2)
                    {
                        Server.PrintToChatAll($"{_prefix}玩 家 \x10{playerName}\x01 切 換 到 \x10 觀 戰 \x01 比 賽 已 中 止");
                        
                        AddTimer(3.0f, () => {
                            if (!_isResetting && !_isMatchEnded)
                                Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10 !R \x01 重 新 對 戰 開 始");
                        });
                    }
                });
                AddTimer(1.2f, () => HandlePlayerLeave(playerName, false, steamId));
            }
            return HookResult.Continue;
        });
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        player.PrintToChat($" {ChatColors.Orange}可 在 聊 天 欄 位 輸 入 您 要 的 武 器，以 下 是 常 用 武 器");
        player.PrintToChat($" -----------------------------------------------------------------");
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ]  {ChatColors.Blue}!dg {ChatColors.White}[ 沙漠之鷹 ]   、 {ChatColors.Blue}!usp {ChatColors.White}[ USP-S ]   、 {ChatColors.Blue}!gk {ChatColors.White}[ 格洛克 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!ak {ChatColors.White}[ AK-47 ]   、 {ChatColors.Green}!a1 {ChatColors.White}[ M4-A1 ]   、 {ChatColors.Green}!a4 {ChatColors.White}[ M4-A4 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ]   、 {ChatColors.Orange}!awp {ChatColors.White}[ 狙擊步槍 ]");
    }

    private void HandlePlayerLeave(string playerName, bool isDisconnect, ulong steamId)
    {
        // 判斷熱身，熱身時絕對不重啟
        if (IsInWarmup() || _isResetting || _isMatchEnded) return;

        int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));
        int totalHumanPlayers = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && p.TeamNum >= 1);

        if (activeCount < 2)
        {
            if (totalHumanPlayers == 0) 
            {
                _isResetting = true;
                _disconnectingPlayers.Clear();
                AddTimer(6.0f, () => { ExecuteForceReset(); });
            }
            else if (isDisconnect && activeCount == 1) 
            {
                Server.PrintToChatAll($"{_prefix}玩 家 \x10{playerName}\x01 已 跳 出 \x10 離 線 \x01 比 賽 已 中 止");
                AddTimer(3.0f, () => {
                    if (!_isResetting && !_isMatchEnded)
                        Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10!R \x01重 新 對 戰 開 始");
                });
            }
        }
    }

    private void ExecuteForceReset()
    {
        // 最後檢查，確保不是在熱身模式下重啟地圖
        if (IsInWarmup()) 
        {
            _isResetting = false;
            return;
        }

        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");
        _isResetting = false;
        _isMatchEnded = false;
    }
}
