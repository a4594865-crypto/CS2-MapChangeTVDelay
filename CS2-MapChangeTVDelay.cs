using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using static CounterStrikeSharp.API.Core.Listeners;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Timers; // 確保有引用 Timer

namespace CS2MapChangeStopTV;

public class MapChangeStopTV : BasePluginConfig
{
    [JsonPropertyName("Debug")] public bool Debug { get; set; } = true;
}

public class CS2MapChangeStopTV : BasePlugin, IPluginConfig<MapChangeStopTV>
{
    public override string ModuleName => "CS2-MapChangeStopTV-UltimateFix";
    public override string ModuleVersion => "0.0.8"; // 版本更新
    public override string ModuleAuthor => "Letaryat & Gemini";
    
    public required MapChangeStopTV Config { get; set; }
    public void OnConfigParsed(MapChangeStopTV config) { Config = config; }

    public override void Load(bool hotReload)
    {
        LogDebug("CS2-MapChangeStopTV (暴力修復版) - Loaded");

        // 1. 【核心功能】解決「換圖不讀 cfg」的問題：新地圖載入後自動開回 GOTV
        RegisterListener<OnMapStart>(mapName =>
        {
            // 延遲 5 秒，確保地圖實體完全載入後再重新掛載 GOTV 模組
            AddTimer(5.0f, () => 
            {
                Server.ExecuteCommand("tv_enable 1");
                Server.ExecuteCommand("tv_broadcast 0"); // 確保廣播保持關閉，預防 #311 提到的 Bug
                LogDebug("新地圖載入：已強行重啟 GOTV 模組 (tv_enable 1)。");
            });
        });

        // 2. 攔截所有可能的換圖指令 (防止手動換圖時當機)
        AddCommandListener("changelevel", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("host_workshop_map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("ds_workshop_changelevel", ListenerChangeLevel, HookMode.Pre);

        // 3. 監聽比賽結束事件 (官方 UI 打勾換圖前的最後攔截點)
        RegisterEventHandler<EventCsWinPanelMatch>((e, i) =>
        {
            LogDebug("結算面板已顯示，0.1 秒後執行卸載程序...");

            AddTimer(0.1f, () => 
            {
                ForceShutdownTV();
            });

            return HookResult.Continue;
        });

        // 4. 監聽地圖結束 (最後一道防線)
        RegisterListener<OnMapEnd>(() =>
        {
            ForceShutdownTV();
        });
    }

    public override void Unload(bool hotReload)
    {
        ForceShutdownTV();
    }

    public HookResult ListenerChangeLevel(CCSPlayerController? player, CommandInfo info)
    {
        LogDebug("偵測到換圖指令，執行暴力卸載以防 10 人環境閃退...");
        ForceShutdownTV();
        return HookResult.Continue;
    }

    public void LogDebug(string message)
    {
        if (Config.Debug == false) { return; }
        Logger.LogInformation($"[StopTV-Fix] {message}");
    }

    // 重新命名為 ForceShutdownTV，因為我們現在不只停錄影，還要「拔模組」
    public void ForceShutdownTV()
    {
        // 根據 MatchZy #311 與 CSS #1239 討論出的解決方案：
        
        // 1. 停止錄影寫入
        Server.ExecuteCommand("tv_stoprecord");
        
        // 2. 徹底關閉廣播模組 (預防網路緩衝區鎖死)
        Server.ExecuteCommand("tv_broadcast 0");
        
        // 3. 最關鍵：徹底銷毀 GOTV 物件 (tv_enable 0)
        // 這能讓 10 人份的實體數據在換圖前被完全抹除，避開記憶體溢位
        Server.ExecuteCommand("tv_enable 0");
        
        LogDebug("已暴力執行：tv_stoprecord, tv_broadcast 0, tv_enable 0 (環境已清空)。");
    }
}
