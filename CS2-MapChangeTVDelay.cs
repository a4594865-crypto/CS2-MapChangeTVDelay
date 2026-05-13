using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線/武器選單8秒重啟";
    public override string ModuleVersion => "1.6.9";

    private readonly string _prefix = " [\x06 1 v 1 對 戰 模 式 \x06] \x01";
    
    private bool _isResetting = false;
    private bool _isMatchEnded = false;

    public override void Load(bool hotReload)
    {
        // 註冊指令 !gs / !GS
        AddCommand("css_gs", "顯示武器選單並觸發重置", OnGsCommand);

        // 1. 監控比賽結束
        RegisterEventHandler<EventCsWinPanelMatch>((@event, info) =>
        {
            _isMatchEnded = true;
            return HookResult.Continue;
        });

        // 2. 監控玩家斷線
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            if (@event.Userid == null || _isMatchEnded || _isResetting) return HookResult.Continue;
            string name = @event.Userid.PlayerName;
            AddTimer(1.5f, () => HandlePlayerLeave(name, false)); 
            return HookResult.Continue;
        });

        // 3. 監控玩家換隊
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            if (@event.Oldteam > 1 && @event.Userid != null && !_isMatchEnded && !_isResetting) 
            {
                string name = @event.Userid.PlayerName;
                AddTimer(0.2f, () => HandlePlayerLeave(name, false));
            }
            return HookResult.Continue;
        });
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;

        // --- 發送私人武器選單訊息 ---
        player.PrintToChat($" 您可在聊天欄位輸入您要的武器，以下是常用武器");
        player.PrintToChat($"{ChatColors.Green}---------------------------------------------------------------");
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ]  {ChatColors.Blue}!dg {ChatColors.White}[ 沙漠之鷹 ]   、 {ChatColors.Blue}!usp {ChatColors.White}[ USP-S ]   、 {ChatColors.Blue}!gk {ChatColors.White}[ 格洛克 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!ak {ChatColors.White}[ AK-47 ]   、 {ChatColors.Green}!a1 {ChatColors.White}[ M4-A1 ]   、 {ChatColors.Green}!a4 {ChatColors.White}[ M4-A4 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ]   、 {ChatColors.Orange}!awp {ChatColors.White}[ 狙擊步槍 ]");

        // --- 觸發重置檢查 ---
        if (_isResetting) return;
        HandlePlayerLeave(player.PlayerName, true); 
    }

    private void HandlePlayerLeave(string playerName, bool isManual)
    {
        if (_isMatchEnded || _isResetting) return;

        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null || gameRules.WarmupPeriod) return;

        var activePlayers = Utilities.GetPlayers().Where(p => 
            p != null && p.IsValid && !p.IsBot && p.SteamID > 0 && (p.TeamNum == 2 || p.TeamNum == 3)
        ).ToList();

        // 如果是手動輸入，或人數不足 2 人
        if (isManual || activePlayers.Count < 2)
        {
            _isResetting = true;

            string reason = isManual ? " \x04手動要求重置\x01 " : " 離開 (\x04 斷 線 / 觀 戰 \x01)";
            Server.PrintToChatAll($"{_prefix}玩家 \x04{playerName}\x01{reason}，比賽中止。");
            Server.PrintToChatAll($"{_prefix}伺服器將在 \x04 5 秒\x01 後「將重新啟動」...");
            
            AddTimer(5.0f, () => {
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
