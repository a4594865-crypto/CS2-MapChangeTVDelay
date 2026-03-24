using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using static CounterStrikeSharp.API.Core.Listeners;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Timers;

namespace CS2MapChangeStopTV;

public class MapChangeStopTV : BasePluginConfig
{
    [JsonPropertyName("Debug")] public bool Debug { get; set; } = true;
}

public class CS2MapChangeStopTV : BasePlugin, IPluginConfig<MapChangeStopTV>
{
    public override string ModuleName => "CS2-MapChangeStopTV-UltimateFix";
    public override string ModuleVersion => "0.0.9"; // 針對 Log 訊息加入語音緩衝清理
    public override string ModuleAuthor => "Letaryat & Gemini";
    
    public required MapChangeStopTV Config { get; set; }
    public void OnConfigParsed(MapChangeStopTV config) { Config = config; }

    public override void Load(bool hotReload)
    {
        LogDebug("CS2-MapChangeStopTV (暴力修復版 v0.0.9) - Loaded");

        // 1. 解決「換圖不讀 cfg」：新地圖載入後，手動開回所有模組
        RegisterListener<OnMapStart>(mapName =>
        {
            // 延遲 5 秒，避開地圖載入最忙碌的瞬間
            AddTimer(5.0f, () => 
            {
                Server.ExecuteCommand("sv_voiceenable 1"); // 恢復語音
                Server.ExecuteCommand("tv_enable 1");      // 恢復 GOTV
                Server.ExecuteCommand("tv_broadcast 0");   // 確保廣播保持關閉 (預防 #311)
                LogDebug("新地圖載入：已強行恢復 TV 與 語音模組。");
            });
        });

        // 2. 攔截換圖指令
        AddCommandListener("changelevel", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("host_workshop_map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("ds_workshop_changelevel", ListenerChangeLevel, HookMode.Pre);

        // 3. 監聽比賽結束事件 (攔截點)
        RegisterEventHandler<EventCsWinPanelMatch>((e, i) =>
        {
            LogDebug("結算面板已顯示，0.1 秒後執行暴力卸載程序...");
            AddTimer(1.0f, ForceShutdownTV);
            return HookResult.Continue;
        });

        // 4. 監聽地圖結束 (最後一道防線)
        RegisterListener<OnMapEnd>(ForceShutdownTV);
    }

    public override void Unload(bool hotReload)
    {
        ForceShutdownTV();
    }

    public HookResult ListenerChangeLevel(CCSPlayerController? player, CommandInfo info)
    {
        LogDebug("偵測到換圖指令，立即清空環境...");
        ForceShutdownTV();
        return HookResult.Continue;
    }

    public void LogDebug(string message)
    {
        if (Config.Debug == false) { return; }
        Logger.LogInformation($"[StopTV-Fix] {message}");
    }

    public void ForceShutdownTV()
    {
        // 根據你的 Log 顯示，CHLTVServer 和 CPlayerVoiceListener 是閃退前的最後訊息
        
        // 1. 停止錄影
        Server.ExecuteCommand("tv_stoprecord");
        
        // 2. 關閉廣播
        Server.ExecuteCommand("tv_broadcast 0");

        // 3. 【新增】提前關閉語音：預防 Log 中的 PostSpawnGroupUnload 死鎖
        Server.ExecuteCommand("sv_voiceenable 0");
        
        // 4. 【核心】提前殺掉 TV 模組：預防 CHLTVServer::Shutdown 撞車
        Server.ExecuteCommand("tv_enable 0");
        
        LogDebug("環境已清空：錄影、廣播、語音、TV 模組已全數強行卸載。");
    }
}
