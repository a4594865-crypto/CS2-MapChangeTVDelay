using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using System;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 武器提示與聊天顯示";
    public override string ModuleVersion => "1.2.0"; 

    // 用你當初最讚的想法：當地圖要關閉/換圖時，用這個開關把聊天功能徹底灌昏
    private bool _isServerShuttingDown = false; 

    public override void Load(bool hotReload)
    {
        _isServerShuttingDown = false;

        // 註冊武器提示指令
        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        // 監聽聊天室訊息
        AddCommandListener("say", OnPlayerSay);
        AddCommandListener("say_team", OnPlayerSay);

        // 🎯 註冊官方換圖/關圖事件：一旦換圖，立刻啟動保護傘
        RegisterEventHandler<EventMapShutdown>((@event, info) => {
            _isServerShuttingDown = true;
            return HookResult.Continue;
        });
    }

    private HookResult OnPlayerSay(CCSPlayerController? player, CommandInfo info)
    {
        // 🛡️ 如果伺服器正在換圖/卸載，直接跳出，絕不干擾官方換圖流程
        if (_isServerShuttingDown || player == null || !player.IsValid) return HookResult.Continue;

        string message = info.GetArg(1).Trim(); 
        string playerName = player.PlayerName;

        if (string.IsNullOrWhiteSpace(message)) return HookResult.Continue;

        // 如果是指令（! 或 / 開頭），交給官方或其他插件處理
        if (message.StartsWith("!") || message.StartsWith("/")) return HookResult.Continue;

        string senderPrefix = $" {ChatColors.White}[所有人]{ChatColors.White}";
        string nameColor = $"{ChatColors.White}";

        // 根據隊伍設定名字顏色
        if (player.TeamNum == 1) nameColor = $"{ChatColors.Grey}";       // 觀戰
        else if (player.TeamNum == 2) nameColor = $"\x10";               // T隊
        else if (player.TeamNum == 3) nameColor = $"\x0B";               // CT隊

        // 全服廣播聊天訊息
        Server.PrintToChatAll($"{senderPrefix} {nameColor}{playerName}{ChatColors.White}：{message}");

        // 同步印到伺服器黑視窗（Console）
        string teamLabel = player.TeamNum == 1 ? "Spec" : (player.TeamNum == 2 ? "TS" : "CT");
        Console.WriteLine($"[{teamLabel}]{playerName}：{message}");

        return HookResult.Handled;
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        
        player.PrintToChat($" {ChatColors.Orange}可 在 聊 天 欄 位 輸 入 您 要 的 武 器，以 下 是 常 用 武 器");
        player.PrintToChat($" -----------------------------------------------------------------");
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ]  {ChatColors.Blue}!dg {ChatColors.White}[ 沙漠之鷹 ]     、 {ChatColors.Blue}!usp {ChatColors.White}[ USP-S ]     、 {ChatColors.Blue}!gk {ChatColors.White}[ 格洛克 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ]     、 {ChatColors.Orange}!awp {ChatColors.White}[ 狙擊步槍 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!ak {ChatColors.White}[ AK-47 ]     、 {ChatColors.Green}!a1 {ChatColors.White}[ M4-A1 ]     、 {ChatColors.Green}!a4 {ChatColors.White}[ M4-A4 ]");
    }

    public override void Unload(bool hotReload)
    {
        _isServerShuttingDown = true;
        base.Unload(hotReload);
    }
}
