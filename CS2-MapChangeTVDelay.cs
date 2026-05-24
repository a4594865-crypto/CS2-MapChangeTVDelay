using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using System;

namespace OneVOneReset;

public class OneVOneReset : BasePlugin
{
    public override string ModuleName => "1V1 武器提示與聊天顯示";
    public override string ModuleVersion => "1.2.2"; // 修正字元版本號

    // 用你當初最讚的想法：當地圖要關閉/換圖時，用這個開關把聊天功能徹底灌昏
    private bool _isServerShuttingDown = false; 

    public override void Load(bool hotReload)
    {
        _isServerShuttingDown = false;

        // 註冊武器提示指令 (已修正引號轉義錯誤)
        AddCommand("css_gs", "顯示武器選單提示", OnGsCommand);

        // 監聽聊天室訊息 (已修正引號轉義錯誤)
        AddCommandListener("say", OnPlayerSay);
        AddCommandListener("say_team", OnPlayerSay);

        // 註冊官方換圖/關圖事件：一旦換圖，立刻啟動保護傘
        RegisterEventHandler<EventMapShutdown>((@event, info) => {
            _isServerShuttingDown = true;
            return HookResult.Continue;
        });
    }

    private HookResult OnPlayerSay(CCSPlayerController? player, CommandInfo info)
    {
        // 如果伺服器正在換圖/卸載，直接跳出，絕不干擾官方換圖流程
        if (_isServerShuttingDown || player == null || !player.IsValid) return HookResult.Continue;

        string message = info.ArgString.Trim('\"', ' ');

        // 如果是空白訊息，或者原本就是指令（開頭是 ! 或 / 或 .），就交給官方與其他插件處理，這裡不重複包裝聊天
        if (string.IsNullOrEmpty(message) || message.StartsWith("!") || message.StartsWith("/") || message.StartsWith("."))
        {
            return HookResult.Continue;
        }

        string playerName = player.PlayerName;
        ChatColors nameColor = ChatColors.Green; // 預設名字顏色

        // 根據隊伍判斷名字顏色
        if (player.TeamNum == 2) // T 隊
        {
            nameColor = ChatColors.LightRed;
        }
        else if (player.TeamNum == 3) // CT 隊
        {
            nameColor = ChatColors.Team;
        }
        else if (player.TeamNum == 1) // 觀察者 Spec
        {
            nameColor = ChatColors.Gray;
        }

        // 保持現狀：完美在遊戲內聊天框顯示自訂格式 (已修正引號轉義錯誤)
        Server.PrintToChatAll($" {nameColor}{playerName}{ChatColors.White}：{message}");

        // 轉印到黑視窗的 Console.WriteLine 已完全移除，不再吃 CPU 效能

        return HookResult.Handled;
    }

    private void OnGsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        
        // 以下所有 PrintToChat 的引號皆已修正完畢，無反斜線
        player.PrintToChat($" {ChatColors.Orange}可 在 聊 天 欄 位 輸 入 您 要 的 武 器，以 下 是 常 用 武 器");
        player.PrintToChat($" ---------------------------------------------------------------------");
        player.PrintToChat($" [ {ChatColors.Blue}手槍{ChatColors.White} ]  {ChatColors.Blue}!dg {ChatColors.White}[ 沙鷹 ] 、{ChatColors.Blue}!usp {ChatColors.White}[ USP ] 、{ChatColors.Blue}!gk {ChatColors.White}[ 格洛克 ] 、{ChatColors.Blue}!r8 {ChatColors.White}[ R8 ]");
        player.PrintToChat($" [ {ChatColors.Orange}狙擊{ChatColors.White} ] {ChatColors.Orange}!ssg {ChatColors.White}[ SSG 08 鳥狙 ] 、{ChatColors.Orange}!awp {ChatColors.White}[ AWP狙擊步槍 ]");
        player.PrintToChat($" [ {ChatColors.Green}步槍{ChatColors.White} ] {ChatColors.Green}!gr {ChatColors.White}[ 咖哩 ] 、{ChatColors.Green}!famas {ChatColors.White}[ 法瑪斯 ] 、{ChatColors.Green}!ak {ChatColors.White}[ AK47 ] 、{ChatColors.Green}!m4 {ChatColors.White}[ M4A4 ] 、{ChatColors.Green}!m4a1 {ChatColors.White}[ M4A1-S ]");
        player.PrintToChat($" ---------------------------------------------------------------------");
    }
}
