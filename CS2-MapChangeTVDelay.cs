using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using static CounterStrikeSharp.API.Core.Listeners;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Timers;

namespace CS2MapChangeStopTV;

public class MapChangeStopTVConfig : BasePluginConfig
{
    [JsonPropertyName("Debug")] public bool Debug { get; set; } = true;
    [JsonPropertyName("MapChangeCooldown")] public float MapChangeCooldown { get; set; } = 180.0f; // 換圖後 180 秒內禁止換圖
}

public class CS2MapChangeStopTV : BasePlugin, IPluginConfig<MapChangeStopTVConfig>
{
    public override string ModuleName => "CS2-MapChangeStopTV-UltimateFix";
    public override string ModuleVersion => "0.1.3"; // 排除伺服器啟動攔截版
    public override string ModuleAuthor => "Letaryat & Gemini";
    
    public required MapChangeStopTVConfig Config { get; set; }
    private DateTime _lastMapStartTime; 

    public void OnConfigParsed(MapChangeStopTVConfig config) { Config = config; }

    public override void Load(bool hotReload)
    {
        LogDebug($"CS2-MapChangeStopTV {ModuleVersion} - 載入成功");
        _lastMapStartTime = DateTime.Now;

        // 1. 地圖啟動邏輯
        RegisterListener<OnMapStart>(mapName =>
        {
            _lastMapStartTime = DateTime.Now; 
            LogDebug($"地圖 {mapName} 開始，進入 {Config.MapChangeCooldown} 秒保護期。");

            AddTimer(7.0f, () => 
            {
                Server.ExecuteCommand("sv_voiceenable 1"); 
                Server.ExecuteCommand("tv_enable 1");      
                Server.ExecuteCommand("tv_broadcast 0");   
                LogDebug("新地圖載入：已強行恢復 TV 與 語音模組。");
            });
        });

        // 2. 攔截原生換圖指令
        AddCommandListener("changelevel", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("host_workshop_map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("ds_workshop_changelevel", ListenerChangeLevel, HookMode.Pre);

        // 3. 攔截 MatchZy 的聊天換圖指令
        AddCommandListener("say", ListenerChatCommand, HookMode.Pre);
        AddCommandListener("say_team", ListenerChatCommand, HookMode.Pre);
        
        // 4. 攔截 MatchZy 控制台換圖指令
        AddCommandListener("css_map", ListenerChangeLevel, HookMode.Pre);
        AddCommandListener("css_workshop", ListenerChangeLevel, HookMode.Pre);

        // 5. 監聽比賽結束
        RegisterEventHandler<EventCsWinPanelMatch>((e, i) =>
        {
            LogDebug("結算面板已顯示，1.0 秒後執行暴力卸載程序...");
            AddTimer(0.3f, ForceShutdownTV);
            return HookResult.Continue;
        });

        // 6. 地圖結束最後防線
        RegisterListener<OnMapEnd>(ForceShutdownTV);
    }

    public override void Unload(bool hotReload)
    {
        ForceShutdownTV();
    }

    public HookResult ListenerChatCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return HookResult.Continue;

        string msg = info.GetArg(1).Trim().ToLower();

        if (msg.StartsWith(".map") || msg.StartsWith("!map"))
        {
            return CheckCooldownAndProcess(player);
        }

        return HookResult.Continue;
    }

    public HookResult ListenerChangeLevel(CCSPlayerController? player, CommandInfo info)
    {
        return CheckCooldownAndProcess(player);
    }

    // 統一的時間檢查邏輯 (已優化來源判斷)
    private HookResult CheckCooldownAndProcess(CCSPlayerController? player)
    {
        // --- 關鍵修改：如果是伺服器本體 (player 為 null)，直接放行 ---
        if (player == null || !player.IsValid) 
        {
            LogDebug("偵測到伺服器本體/系統發出換圖指令，直接放行不攔截。");
            ForceShutdownTV(); // 換圖前仍需清理 TV 模組避免閃退
            return HookResult.Continue; 
        }

        // --- 以下僅對「真實玩家/管理員」進行冷卻檢查 ---
        double secondsSinceStart = (DateTime.Now - _lastMapStartTime).TotalSeconds;

        if (secondsSinceStart < Config.MapChangeCooldown)
        {
            float timeLeft = Config.MapChangeCooldown - (float)secondsSinceStart;
            
            // 給在線的管理員提示
            player.PrintToChat($" [\x04系統訊息\x01] \x01換圖保護中！請等待 \x04{timeLeft:F0} \x01秒後再使用 \x04換圖指令\x01。");

            LogDebug($"攔截玩家 {player.PlayerName} 指令：冷卻剩餘 {timeLeft:F0} 秒");
            return HookResult.Handled; 
        }

        LogDebug($"冷卻已過，執行玩家 {player.PlayerName} 的換圖清理...");
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
        Server.ExecuteCommand("tv_stoprecord");
        Server.ExecuteCommand("tv_broadcast 0");
        Server.ExecuteCommand("sv_voiceenable 0");
        Server.ExecuteCommand("tv_enable 0");
        LogDebug("環境已清空：錄影、廣播、語音、TV 模組已全數強行卸載。");
    }
}
