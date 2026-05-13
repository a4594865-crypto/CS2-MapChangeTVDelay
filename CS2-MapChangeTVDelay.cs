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
    public override string ModuleVersion => "1.8.2";

    private readonly string _prefix = " [\x04 1 v 1 對 戰 模 式 \x01] ";
    
    private bool _isResetting = false;
    private bool _isMatchEnded = false;
    private DateTime _lastResetTime = DateTime.MinValue;
    private const int CooldownSeconds = 360;

    public override void Load(bool hotReload)
    {
        _lastResetTime = DateTime.Now;

        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) =>
        {
            _isMatchEnded = true;
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            if (@event.Userid == null || _isMatchEnded || _isResetting) return HookResult.Continue;
            string playerName = @event.Userid.PlayerName;
            AddTimer(1.5f, () => HandlePlayerLeave(playerName, true)); 
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Userid == null || _isMatchEnded || _isResetting) return HookResult.Continue;

            if (@event.Oldteam > 1 && @event.Team == 1)
            {
                string playerName = @event.Userid.PlayerName;
                Server.PrintToChatAll($"{_prefix}玩家 \x10{playerName}\x01 切 換 到 觀 戰  比賽已中止");
                Server.PrintToChatAll($"{_prefix}請下一組玩家輸入 \x10!R \x01重新對戰開始");
                Console.WriteLine($"[1V1 Log] 玩家 {playerName} 切換到觀戰比賽已中止。");
            }
            else if (@event.Oldteam > 1)
            {
                string playerName = @event.Userid.PlayerName;
                AddTimer(0.2f, () => HandlePlayerLeave(playerName, false)); 
            }
            return HookResult.Continue;
        });
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        player.PrintToChat($" {ChatColors.Orange}可 在 聊 天 欄 位 輸 入 您 要 的 武 器，以 下 是 常 用 武 器");
        player.PrintToChat($" -----------------------------------------------------------------");
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ]  {ChatColors.Blue}!dg {ChatColors.White}[ 沙漠之鷹 ]   、 {ChatColors.Blue}!usp {ChatColors.White}[ USP-S ]   、 {ChatColors.Blue}!gk {ChatColors.White}[ 格洛克 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!ak {ChatColors.White}[ AK-47 ]   、 {ChatColors.Green}!a1 {ChatColors.White}[ M4-A1 ]   、 {ChatColors.Green}!a4 {ChatColors.White}[ M4-A4 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ]   、 {ChatColors.Orange}!awp {ChatColors.White}[ 狙擊步槍 ]");
    }

    private void HandlePlayerLeave(string playerName, bool isDisconnect)
    {
        if (_isResetting || _isMatchEnded) return;

        // 偵測 CT 與 T 隊的人數
        int activeCount = Utilities.GetPlayers().Count(p => 
            p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3)
        );

        if (activeCount < 2)
        {
            var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
            // 如果沒找到規則或是還在熱身期，不處理
            if (gameRules == null || gameRules.WarmupPeriod) return;

            if (isDisconnect)
            {
                double secondsSinceLastReset = (DateTime.Now - _lastResetTime).TotalSeconds;
                
                // 【核心邏輯更新】
                // 如果場上還剩 1 個人，且還在 360 秒冷卻內，則不允許重啟
                if (activeCount == 1 && secondsSinceLastReset < CooldownSeconds)
                {
                    int remaining = CooldownSeconds - (int)secondsSinceLastReset;
                    Server.PrintToChatAll($"{_prefix} \x10系統保護 \x01重啟冷卻中，剩餘 \x04{remaining}\x01 秒。");
                    return;
                }
                
                // 如果 activeCount == 0 (沒人了)，不論冷卻多久，都會執行下面的重啟代碼

                _isResetting = true;
                _lastResetTime = DateTime.Now;

                Server.PrintToChatAll($"{_prefix}玩家 \x10{playerName}\x01 離開 (\x10 斷 線 \x01) 比賽中止");
                Server.PrintToChatAll($"{_prefix}伺服器將在 \x10 5 秒\x01 後「重新重置啟動」...");
                
                Console.WriteLine($"[1V1 Log] 偵測到斷線 ({playerName})，剩餘人數: {activeCount}，觸發重置。");
                
                AddTimer(6.0f, () => { ExecuteForceReset(); });
            }
        }
    }

    private void ExecuteForceReset()
    {
        Server.ExecuteCommand("mp_backup_restore_load_autobackup 0");
        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");
        
        _isResetting = false;
        _isMatchEnded = false;
    }
}
