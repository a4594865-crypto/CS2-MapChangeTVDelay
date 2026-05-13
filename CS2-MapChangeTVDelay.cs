using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using System.Linq;
using System; // 引用 System 以使用 DateTime

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 防惡意重啟/!gs武器提示";
    public override string ModuleVersion => "1.7.5";

    private readonly string _prefix = " [\x04 1 v 1 對 戰 模 式 \x01] ";
    
    private bool _isResetting = false;
    private bool _isMatchEnded = false;

    // --- 新增：記錄上一次成功執行重啟的時間 ---
    private static DateTime _lastResetTime = DateTime.MinValue;
    // 設定冷卻秒數（例如 60 秒）
    private const int CooldownSeconds = 120;

    public override void Load(bool hotReload)
    {
        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) =>
        {
            _isMatchEnded = true;
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            if (@event.Userid == null || _isMatchEnded || _isResetting) return HookResult.Continue;
            string name = @event.Userid.PlayerName;
            AddTimer(1.5f, () => HandlePlayerLeave(name)); 
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Oldteam > 1 && @event.Userid != null && !_isMatchEnded && !_isResetting) 
            {
                string name = @event.Userid.PlayerName;
                AddTimer(0.2f, () => HandlePlayerLeave(name));
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

    private void HandlePlayerLeave(string playerName)
    {
        if (_isResetting || _isMatchEnded) return;

        // 【新增：冷卻檢查】
        double secondsSinceLastReset = (DateTime.Now - _lastResetTime).TotalSeconds;
        if (secondsSinceLastReset < CooldownSeconds)
        {
            // 如果還在冷卻中，噴出提示並直接結束，不執行重啟
            int remaining = CooldownSeconds - (int)secondsSinceLastReset;
            Server.PrintToChatAll($"{_prefix} \x10系統保護 \x01重啟冷卻中 ，請等待 \x04{remaining}\x01 秒。");
            return;
        }

        int activeCount = Utilities.GetPlayers().Count(p => 
            p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3)
        );

        if (activeCount < 2)
        {
            var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
            if (gameRules == null || gameRules.WarmupPeriod) return;

            _isResetting = true;
            // 更新最後重啟時間
            _lastResetTime = DateTime.Now;

            Server.PrintToChatAll($"{_prefix}玩家 \x10{playerName}\x01 離開 (\x10 斷 線 / 觀 戰 \x01) 比賽中止");
            Server.PrintToChatAll($"{_prefix}伺服器將在 \x10 5 秒\x01 後「重新重置啟動」...");
            
            AddTimer(6.0f, () => 
            {
                ExecuteForceReset();
            });
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
