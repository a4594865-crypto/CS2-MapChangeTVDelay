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

        // [終極解決方案] 拋棄不穩定的事件監聽，直接監聽伺服器 say 和 say_team 全體打字指令
        AddCommandListener("say", OnPlayerSay);
        AddCommandListener("say_team", OnPlayerSay);

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

    // 處理打字指令的專用方法
// 處理打字指令的專用方法
private HookResult OnPlayerSay(CCSPlayerController? player, CommandInfo info)
{
    if (player == null || !player.IsValid) return HookResult.Continue;

    // 抓取字串最乾淨、最省效能且最安全
    string message = info.GetArg(1).Trim(); 
    string playerName = player.PlayerName;

    if (string.IsNullOrWhiteSpace(message)) return HookResult.Continue;

    // 如果是指令開頭(如 !r, !ak, /r)，跳過不處理，放行給系統和其他插件
    if (message.StartsWith("!") || message.StartsWith("/")) return HookResult.Continue;

    // 讓 [所有人] 前綴保持乾淨的白字
    string senderPrefix = $" {ChatColors.White}[所有人]{ChatColors.White}";
    
    // 預設名字顏色為白色
    string nameColor = $"{ChatColors.White}";

    // 分成三個隊伍手動強制給顏色：
    // 1 = 觀戰者 (Spectator) -> 修正為原生灰色
    // 2 = T (Terrorist)       -> 橘紅色
    // 3 = CT (Counter-Terrorist) -> 亮藍色
    if (player.TeamNum == 1)
    {
        nameColor = $"{ChatColors.Grey}"; // 觀戰灰
    }
    else if (player.TeamNum == 2)
    {
        nameColor = $"\x09"; // T 橘紅
    }
    else if (player.TeamNum == 3)
    {
        nameColor = $"\x0A"; // 正宗 CT 亮藍
    }

    // 強制全體廣播：[所有人] 名字(強制色彩) : 訊息(白字)
    Server.PrintToChatAll($"{senderPrefix} {nameColor}{playerName}{ChatColors.White}：{message}");

    // 阻斷原本的聊天訊息，避免畫面上出現兩次
    return HookResult.Handled;
}
    public override void Unload(bool hotReload)
    {
        _isServerShuttingDown = true;
        base.Unload(hotReload);
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        
        player.PrintToChat($" {ChatColors.Orange}可 在 聊天 欄 位 輸 入 您 要 的 武器，以 下 是 常 用 武器");
        player.PrintToChat($" -----------------------------------------------------------------");
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ]  {ChatColors.Blue}!dg {ChatColors.White}[ 沙漠之鷹 ]     、 {ChatColors.Blue}!usp {ChatColors.White}[ USP-S ]     、 {ChatColors.Blue}!gk {ChatColors.White}[ 格洛克 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ]     、 {ChatColors.Orange}!awp {ChatColors.White}[ 狙擊步槍 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!ak {ChatColors.White}[ AK-47 ]     、 {ChatColors.Green}!a1 {ChatColors.White}[ M4-A1 ]     、 {ChatColors.Green}!a4 {ChatColors.White}[ M4-A4 ]");
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
                Server.PrintToChatAll($"{_prefix}請 下 一 組 玩家 輸 入 \x10 !R \x01 重 新 對 戰 開 始");
            }
        }
    }
}
