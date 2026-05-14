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
    public override string ModuleVersion => "2.1.2"; 

    private readonly string _prefix = " [\x04 1 v 1 對 戰 模 式 \x01] ";
    private bool _isResetting = false;
    private bool _isMatchEnded = false;

    public override void Load(bool hotReload)
    {
        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        // --- 修正處：將 EventMatchStart 改為 EventRoundAnnounceMatchStart ---
        // 這能捕捉到比賽正式宣告開始的瞬間（包含補位玩家輸入 !R 後的啟動）
        RegisterEventHandler<EventRoundAnnounceMatchStart>((@event, info) => {
            Console.WriteLine($"[1V1 Log] >>> 玩家已輸入 !R，比賽正式啟動 (VProfLite 同步檢測中) <<<");
            _isMatchEnded = false; 
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) => {
            if (@event.Userid == null || _isMatchEnded || _isResetting) return HookResult.Continue;
            var player = @event.Userid;
            if (player.TeamNum <= 1) return HookResult.Continue;

            AddTimer(1.5f, () => HandlePlayerLeave(player.PlayerName, true)); 
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) => {
            if (@event.Userid == null || !@event.Userid.IsValid || _isMatchEnded || _isResetting) 
                return HookResult.Continue;

            var player = @event.Userid;
            // 偵測從對戰位 (CT/T) 切換到觀戰位
            if (@event.Oldteam > 1 && @event.Team <= 1)
            {
                AddTimer(0.1f, () => {
                    int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));
                    if (activeCount < 2)
                    {
                        Server.PrintToChatAll($"{_prefix}玩 家 \x10{player.PlayerName}\x01 切 換 到 \x10觀 戰 \x01比 賽 已 中 止");
                        Console.WriteLine($"[1V1 Log] 玩家 {player.PlayerName} 跳往觀戰，比賽中止。");
                        
                        AddTimer(4.0f, () => {
                            if (!_isResetting && !_isMatchEnded)
                                Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10!R \x01重 新 對 戰 開 始");
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
        player.PrintToChat($" {ChatColors.Orange}可 在 聊 天 欄 位 輸 入 您 要 的 武 器，以 下 是 常 用 武 器");
        player.PrintToChat($" -----------------------------------------------------------------");
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ]  {ChatColors.Blue}!dg {ChatColors.White}[ 沙漠之鷹 ]   、 {ChatColors.Blue}!usp {ChatColors.White}[ USP-S ]   、 {ChatColors.Blue}!gk {ChatColors.White}[ 格洛克 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!ak {ChatColors.White}[ AK-47 ]   、 {ChatColors.Green}!a1 {ChatColors.White}[ M4-A1 ]   、 {ChatColors.Green}!a4 {ChatColors.White}[ M4-A4 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ]   、 {ChatColors.Orange}!awp {ChatColors.White}[ 狙擊步槍 ]");
    }

    private void HandlePlayerLeave(string playerName, bool isDisconnect)
    {
        if (_isResetting || _isMatchEnded) return;

        int activeCount = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3));
        int totalHumanPlayers = Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && p.TeamNum >= 1);

        if (activeCount < 2)
        {
            if (totalHumanPlayers == 0) 
            {
                Console.WriteLine($"[1V1 Log] 完全空城，執行重置。");
                _isResetting = true;
                AddTimer(6.0f, () => { ExecuteForceReset(); });
            }
            else if (isDisconnect && activeCount == 1) 
            {
                Server.PrintToChatAll($"{_prefix}玩 家 \x10{playerName}\x01 已 跳 出 \x10 離 線 \x01比 賽 已 中 止");
                Console.WriteLine($"[1V1 Log] 玩家 {playerName} 斷線，比賽中止 (尚有觀戰者，不重啟)。");
                
                AddTimer(4.0f, () => {
                    if (!_isResetting && !_isMatchEnded)
                        Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10!R \x01重 新 對 戰 開 始");
                });
            }
        }
    }

    private void ExecuteForceReset()
    {
        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");
        _isResetting = false;
        _isMatchEnded = false;
        Console.WriteLine($"[1V1 Log] 執行地圖重換，伺服器已自動初始化。");
    }
}
