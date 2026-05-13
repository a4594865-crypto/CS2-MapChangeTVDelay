using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using System.Linq;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 斷線自動重啟/!gs武器提示";
    public override string ModuleVersion => "1.7.2";

    // 插件前綴
    private readonly string _prefix = " [\x04 1 v 1 對 戰 模 式 \x01] ";
    
    // 狀態開關
    private bool _isResetting = false;
    private bool _isMatchEnded = false;

    public override void Load(bool hotReload)
    {
        // 註冊指令 !gs (僅顯示武器訊息，不觸發重置)
        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        // 1. 監控比賽結束 (例如大比分結算時)
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
            // 給予 1.5 秒緩衝確保玩家資料已從伺服器清單移除
            AddTimer(1.5f, () => HandlePlayerLeave(name)); 
            return HookResult.Continue;
        });

        // 3. 監控玩家換隊 (跳觀戰)
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            // 如果從 TR(2) 或 CT(3) 換到觀戰(1) 或其他
            if (@event.Oldteam > 1 && @event.Userid != null && !_isMatchEnded && !_isResetting) 
            {
                string name = @event.Userid.PlayerName;
                // 換隊邏輯較快，0.2 秒即可
                AddTimer(0.2f, () => HandlePlayerLeave(name));
            }
            return HookResult.Continue;
        });
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;

        // 私人訊息：顯示武器選單提示
        player.PrintToChat($" {ChatColors.Orange}可 在 聊 天 欄 位 輸 入 您 要 的 武 器，以 下 是 常 用 武 器");
        player.PrintToChat($" -----------------------------------------------------------------");
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ]  {ChatColors.Blue}!dg {ChatColors.White}[ 沙漠之鷹 ]   、 {ChatColors.Blue}!usp {ChatColors.White}[ USP-S ]   、 {ChatColors.Blue}!gk {ChatColors.White}[ 格洛克 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!ak {ChatColors.White}[ AK-47 ]   、 {ChatColors.Green}!a1 {ChatColors.White}[ M4-A1 ]   、 {ChatColors.Green}!a4 {ChatColors.White}[ M4-A4 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ]   、 {ChatColors.Orange}!awp {ChatColors.White}[ 狙擊步槍 ]");
    }

    private void HandlePlayerLeave(string playerName)
    {
        // 【第一層攔截】檢查狀態布林，極快不吃效能
        if (_isResetting || _isMatchEnded) return;

        // 【第二層攔截】計算對戰人數 (不使用 ToList() 以節省記憶體開銷)
        int activeCount = Utilities.GetPlayers().Count(p => 
            p != null && 
            p.IsValid && 
            !p.IsBot && 
            p.SteamID > 0 && 
            (p.TeamNum == 2 || p.TeamNum == 3)
        );

        // 如果場上還有 2 人以上，代表不需要重啟，直接結束執行
        if (activeCount >= 2) return;

        // 【第三層攔截】獲取 GameRules 實體 (搜尋實體表相對耗時，因此放在最後)
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        if (gameRules == null || gameRules.WarmupPeriod) return;

        // 執行重啟流程
        _isResetting = true;

        Server.PrintToChatAll($"{_prefix}玩家 \x10{playerName}\x01 離開 (\x10 斷 線 / 觀 戰 \x01) 比賽中止");
        Server.PrintToChatAll($"{_prefix}伺服器將在 \x10 5 秒\x01 後「重新啟動重置」...");
        
        AddTimer(6.0f, () => {
            ExecuteForceReset();
        });
    }

    private void ExecuteForceReset()
    {
        // 1. 重置備份系統
        Server.ExecuteCommand("mp_backup_restore_load_autobackup 0");
        // 2. 重新載入地圖
        Server.ExecuteCommand($"ds_workshop_changelevel {Server.MapName}");
        // 3. 確保進入熱身模式
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");
        
        // 狀態重置
        _isResetting = false;
        _isMatchEnded = false;
    }
}
