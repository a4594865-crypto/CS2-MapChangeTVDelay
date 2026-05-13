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
    public override string ModuleVersion => "1.8.8";

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
            
            var player = @event.Userid;
            string playerName = player.PlayerName;
            
            // 如果是在 觀戰席(1) 或 無隊伍(0) 斷線，不觸發重啟邏輯
            if (player.TeamNum <= 1) 
            {
                // 只在黑視窗靜默紀錄
                Console.WriteLine($"[1V1 Log] 觀戰者 {playerName} 離開伺服器，忽略重啟判定。");
                return HookResult.Continue;
            }

            Console.WriteLine($"[1V1 Log] 對戰玩家 {playerName} 斷線，進入重啟判定。");
            AddTimer(1.5f, () => HandlePlayerLeave(playerName, true)); 
            return HookResult.Continue;
        });

        // --- 處理：換隊 / 跳觀戰 ---
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Userid == null || !@event.Userid.IsValid || _isMatchEnded || _isResetting) 
                return HookResult.Continue;

            var player = @event.Userid;
            string playerName = player.PlayerName;
            int oldTeam = @event.Oldteam;
            int newTeam = @event.Team;

            // 邏輯：從對戰席 (2, 3) 離開到 非對戰席 (0, 1)
            if (oldTeam > 1 && newTeam <= 1)
            {
                var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
                bool isWarmup = gameRules?.WarmupPeriod ?? false;

                if (!isWarmup)
                {
                    // 延遲 0.1 秒，等伺服器完成隊伍切換後計算剩餘人數
                    AddTimer(0.1f, () => {
                        int activeCount = Utilities.GetPlayers().Count(p => 
                            p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3)
                        );

                        // 如果走掉之後，對戰席剩下不到 2 人
                        if (activeCount < 2)
                        {
                            Server.PrintToChatAll($"{_prefix}玩 家 \x10{playerName}\x01 切 換 到 \x10觀 戰 \x01比 賽 已 中 止");
                            Console.WriteLine($"[1V1 Log] {playerName} 切換到觀戰，比賽中止");

                            AddTimer(4.0f, () => {
                                if (!_isResetting && !_isMatchEnded)
                                    Server.PrintToChatAll($"{_prefix}請 下 一 組 玩 家 輸 入 \x10!R \x01重 新 對 戰 開 始");
                            });
                        }
                    });
                }
                // 執行 HandlePlayerLeave (內部會處理空城重啟)
                AddTimer(1.2f, () => HandlePlayerLeave(playerName, false));
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

        int activeCount = Utilities.GetPlayers().Count(p => 
            p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3)
        );

        if (activeCount < 2)
        {
            var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
            if (gameRules == null || gameRules.WarmupPeriod) return;

            double secondsSinceLastReset = (DateTime.Now - _lastResetTime).TotalSeconds;
            bool shouldReset = false;

            if (activeCount == 0)
            {
                // 空城重啟，在黑視窗印出 LOG
                Console.WriteLine($"[1V1 Log] 檢測到場上已無對戰玩家，準備執行自動地圖重置。");
                shouldReset = true;
            }
            else if (isDisconnect && activeCount == 1)
            {
                // 斷線重啟
                if (secondsSinceLastReset >= CooldownSeconds)
                {
                    Server.PrintToChatAll($"{_prefix}玩 家 \x10{playerName}\x01 離 開 (\x10 斷 線 \x01) 比 賽 中 止");
                    Server.PrintToChatAll($"{_prefix}伺 服 器 將 在 \x10 5 秒\x01 後「 重 新 重 置 啟 動 」...");
                    shouldReset = true;
                }
                else
                {
                    int remaining = CooldownSeconds - (int)secondsSinceLastReset;
                    Server.PrintToChatAll($"{_prefix} \x101重啟 \x0冷卻中，剩餘 \x04{remaining}\x01 秒。");
                    Console.WriteLine($"[1V1 Log] {playerName} 斷線，但重啟冷卻中。");
                }
            }

            if (shouldReset)
            {
                _isResetting = true;
                _lastResetTime = DateTime.Now;
                Console.WriteLine($"[1V1 Log] 執行地圖更換命令 (ds_workshop_changelevel)。");
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
        Console.WriteLine($"[1V1 Log] 地圖重置完成。");
    }
}
