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
    public override string ModuleVersion => "2.5.3"; 

    private readonly string _prefix = " [\x04 1 v 1 對 戰 模 式 \x01] ";
    private bool _isMatchEnded = false;
    private readonly System.Collections.Generic.HashSet<ulong> _disconnectingPlayers = new();

    private bool IsInWarmup()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        return gameRules == null || gameRules.WarmupPeriod;
    }

    public override void Load(bool hotReload)
    {
        // [功能 1] 完整槍枝顯示指令
        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) => {
            _isMatchEnded = true;
            return HookResult.Continue;
        });

        // [功能 2] 處理玩家離線 Log 與訊息
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) => {
            if (IsInWarmup() || @event.Userid == null || _isMatchEnded) 
                return HookResult.Continue;

            var player = @event.Userid;
            if (!player.IsValid || player.IsBot) return HookResult.Continue;

            // 提前將名字與 SteamID 存成獨立變數，防止延遲後實體消失抓不到
            string cachedPlayerName = player.PlayerName;
            ulong cachedSteamId = player.SteamID;

            if (cachedSteamId > 0) 
            {
                _disconnectingPlayers.Add(cachedSteamId);
            }

            // 斷線時立刻輸出後台 Log，確保不漏資訊
            Console.WriteLine($"[1V1 Log] 偵測到玩家 {cachedPlayerName} ({cachedSteamId}) 正在中斷連線...");

            AddTimer(1.5f, () => {
                // 延遲後直接呼叫客製化的斷線廣播
                HandlePlayerDisconnectMsg(cachedPlayerName);
                _disconnectingPlayers.Remove(cachedSteamId);
            }); 
            return HookResult.Continue;
        });

        // [功能 3] 處理玩家換隊 Log 與訊息 (跳觀戰)
        RegisterEventHandler<EventPlayerTeam>((@event, info) => {
            if (IsInWarmup() || @event.Userid == null || !@event.Userid.IsValid || _isMatchEnded) 
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
                    if (_disconnectingPlayers.Contains(cachedId)) return;

                    int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));
                    if (activeCount < 2)
                    {
                        // 遊戲內廣播
                        Server.PrintToChatAll($"{_prefix}玩 家 \x10{cachedName}\x01 切 換 到 \x10 觀 戰 \x01 比 賽 已 中 止");
                        
                        // 後台 Log
                        Console.WriteLine($"[1V1 Log] 玩家 {cachedName} 切換到觀戰，比賽已中止");
                        
                        AddTimer(3.0f, () => {
                            if (!_isMatchEnded)
                                Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10 !R \x01 重 新 對 戰 開 始");
                        });
                    }
                });
            }
            return HookResult.Continue;
        });
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        
        player.PrintToChat($" {ChatColors.Orange}可 在 聊 天 欄 位 輸 入 您 要 的 武器，以 下 是 常 用 武 器");
        player.PrintToChat($" -----------------------------------------------------------------");
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ]  {ChatColors.Blue}!dg {ChatColors.White}[ 沙漠之鷹 ]   、 {ChatColors.Blue}!usp {ChatColors.White}[ USP-S ]   、 {ChatColors.Blue}!gk {ChatColors.White}[ 格洛克 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!ak {ChatColors.White}[ AK-47 ]   、 {ChatColors.Green}!a1 {ChatColors.White}[ M4-A1 ]   、 {ChatColors.Green}!a4 {ChatColors.White}[ M4-A4 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ]   、 {ChatColors.Orange}!awp {ChatColors.White}[ 狙擊步槍 ]");
    }

    private void HandlePlayerDisconnectMsg(string playerName)
    {
        if (IsInWarmup() || _isMatchEnded) return;

        // 計算目前場上還留在 2(T) 或 3(CT) 隊伍中的真實玩家人數
        int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));

        // 因為有人斷線了，場上活躍人數如果小於 2 (通常剩 1 人)，就觸發中止對戰提示
        if (activeCount < 2)
        {
            // 遊戲內廣播
            Server.PrintToChatAll($"{_prefix}玩 家 \x10{playerName}\x01 已 跳 出 \x10 離 線 \x01 比 賽 已 中 止");
            
            // 後台 Log
            Console.WriteLine($"[1V1 Log] 玩家 {playerName} 斷線離場，比賽已中止");
            
            AddTimer(0.1f, () => {
                if (!_isMatchEnded)
                    Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10 !R \x01 重 新 對 戰 開 始");
            });
        }
    }
}
