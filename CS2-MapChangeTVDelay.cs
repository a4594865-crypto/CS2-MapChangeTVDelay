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
    public override string ModuleVersion => "1.8.4";

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

        // --- 處理：斷線 ---
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            if (@event.Userid == null || _isMatchEnded || _isResetting) return HookResult.Continue;
            string playerName = @event.Userid.PlayerName;
            AddTimer(1.5f, () => HandlePlayerLeave(playerName, true)); 
            return HookResult.Continue;
        });

        // --- 處理：換隊 / 跳觀戰 (優化判定邏輯) ---
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Userid == null || !@event.Userid.IsValid || _isMatchEnded || _isResetting) 
                return HookResult.Continue;

            string playerName = @event.Userid.PlayerName;
            int oldTeam = @event.Oldteam;
            int newTeam = @event.Team;

            // 邏輯：如果原本在打球 (CT:3, T:2)，現在去了觀戰 (1) 或無隊伍 (0)
            if (oldTeam > 1 && newTeam <= 1)
            {
                // 1. 遊戲內公告 (第一行訊息立刻噴出)
                Server.PrintToChatAll($"{_prefix}玩家 \x10{playerName}\x01 切 換 到 觀 戰  \x10比賽已中止");

                // 2. 【延遲顯示】：設定在 4.0 秒後才顯示第二行提醒
                AddTimer(4.0f, () => {
                    Server.PrintToChatAll($"{_prefix}請下一組玩家輸入 \x10!R \x01重新對戰開始");
                });
                
                // 3. 黑視窗紀錄
                Console.WriteLine($"[1V1 Log] 玩家 {playerName} 切 換 到 觀 戰，比賽已中止。");

                // 觸發人數檢查
                AddTimer(1.0f, () => HandlePlayerLeave(playerName, false));
            }
            else if (oldTeam > 1 && newTeam > 1)
            {
                // 單純 CT/T 互換
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
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ]  {ChatColors.Blue}!dg {ChatColors.White}[ 沙漠之鷹 ]   、 {ChatColors.Blue}!usp {ChatColors.White}[ USP-S ]   、 {ChatColors.Blue}!gk {ChatColors.White}[ 格洛克 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!ak {ChatColors.White}[ AK-47 ]   、 {ChatColors.Green}!a1 {ChatColors.White}[ M4-A1 ]   、 {ChatColors.Green}!a4 {ChatColors.White}[ M4-A4 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ]   、 {ChatColors.Orange}!awp {ChatColors.White}[ 狙擊步槍 ]");
    }

    private void HandlePlayerLeave(string playerName, bool isDisconnect)
    {
        if (_isResetting || _isMatchEnded) return;

        // 偵測打球人數
        int activeCount = Utilities.GetPlayers().Count(p => 
            p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3)
        );

        if (activeCount < 2)
        {
            var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
            if (gameRules == null || gameRules.WarmupPeriod) return;

            double secondsSinceLastReset = (DateTime.Now - _lastResetTime).TotalSeconds;

            // --- 情況 1：斷線且場上剩 1 人 (執行 360s 冷卻檢查) ---
            if (isDisconnect && activeCount == 1)
            {
                if (secondsSinceLastReset < CooldownSeconds)
                {
                    int remaining = CooldownSeconds - (int)secondsSinceLastReset;
                    Server.PrintToChatAll($"{_prefix} \x10系統保護 \x01重啟冷卻中，剩餘 \x04{remaining}\x01 秒。");
                    return;
                }
            }

            // --- 情況 2：執行重啟 (斷線且過冷卻，或是場上完全沒人) ---
            if (isDisconnect || activeCount == 0)
            {
                _isResetting = true;
                _lastResetTime = DateTime.Now;

                if (activeCount == 0)
                {
                    Server.PrintToChatAll($"{_prefix}場上已無玩家，伺服器將在 \x10 5 秒\x01 後自動重置...");
                }
                else
                {
                    Server.PrintToChatAll($"{_prefix}玩家 \x10{playerName}\x01 離開 (\x10 斷 線 \x01) 比賽中止");
                    Server.PrintToChatAll($"{_prefix}伺服器將在 \x10 5 秒\x01 後「重新重置啟動」...");
                }
                
                Console.WriteLine($"[1V1 Log] 執行自動重置。觸發者：{playerName}，場上人數：{activeCount}");
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
