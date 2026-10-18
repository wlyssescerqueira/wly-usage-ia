using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

namespace WlyUsage;

internal sealed record UsageWindow(double Used, DateTimeOffset? Reset);
internal sealed record AgentReading(string Name, string Account, string Activity, bool Working, UsageWindow? Five, UsageWindow? Week, DateTimeOffset? Collected, string Source);
internal static class LocalUsage
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static AgentReading[] Read() => new[] { Claude(), Codex() };
    private static JsonElement Get(JsonElement root, string key) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var value) ? value : default;
    private static string Str(JsonElement root, string key) => Get(root, key).ValueKind == JsonValueKind.String ? Get(root, key).GetString() ?? "" : "";
    private static DateTimeOffset? Date(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long epoch))
        {
            try { return epoch > 100000000000 ? DateTimeOffset.FromUnixTimeMilliseconds(epoch) : DateTimeOffset.FromUnixTimeSeconds(epoch); }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        return value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var date) ? date : null;
    }
    private static UsageWindow? Window(JsonElement root, string percent, string reset)
    {
        var number = Get(root, percent); var date = Date(Get(root, reset));
        if (number.ValueKind != JsonValueKind.Number || !number.TryGetDouble(out double used) || !double.IsFinite(used) || date <= DateTimeOffset.UtcNow) return null;
        return new(Math.Clamp(used, 0, 100), date);
    }
    private static string[] Files(string directory, int count)
    {
        if (!Directory.Exists(directory)) return Array.Empty<string>();
        return Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "subagents" + Path.DirectorySeparatorChar))
            .OrderByDescending(File.GetLastWriteTimeUtc).Take(count).ToArray();
    }
    private static IEnumerable<JsonElement> Tail(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        bool partial = stream.Length > 262144; if (partial) stream.Seek(-262144, SeekOrigin.End);
        using var reader = new StreamReader(stream); if (partial) reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(line); } catch (JsonException) { }
            if (doc != null) { using (doc) { yield return doc.RootElement.Clone(); } }
        }
    }
    private static AgentReading Claude()
    {
        UsageWindow? five = null, week = null; DateTimeOffset? collected = null;
        string source = "Sem coleta de cotas", activity = "Sem atividade recente"; bool working = false;
        try
        {
            string dir = Path.Combine(Home, ".habblaud", "usage");
            if (Directory.Exists(dir)) foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                {
                    if (new FileInfo(file).Length > 65536) continue;
                    using var doc = JsonDocument.Parse(File.ReadAllText(file)); var root = doc.RootElement;
                    if (Str(root, "accountId") != ".claude") continue;
                    var time = Date(Get(root, "fetchedAt")); if (time == null || (collected != null && time <= collected)) continue;
                    five = Window(Get(root, "five_hour"), "utilization", "resets_at"); week = Window(Get(root, "seven_day"), "utilization", "resets_at");
                    collected = time; source = "Claude · coleta local de statusline";
                }
            foreach (var file in Files(Path.Combine(Home, ".claude", "projects"), 8))
            {
                bool active = false; DateTimeOffset? latest = null;
                foreach (var item in Tail(file))
                {
                    var type = Str(item, "type"); var time = Date(Get(item, "timestamp"));
                    if (type == "user") { active = true; latest = time; }
                    if (type == "assistant") { var reason = Str(Get(item, "message"), "stop_reason"); active = reason != "end_turn" && reason != "stop_sequence"; latest = time; }
                }
                if (active && latest > DateTimeOffset.UtcNow.AddMinutes(-3)) { working = true; activity = "Trabalhando"; break; }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { source = "Fonte local temporariamente indisponível"; }
        return new("Claude", "Local", activity, working, five, week, collected, source);
    }
    private static AgentReading Codex()
    {
        UsageWindow? five = null, week = null; DateTimeOffset? collected = null;
        string account = "Local", activity = "Sem atividade recente", source = "Sem coleta de cotas"; bool working = false;
        try
        {
            string dir = Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(Home, ".codex");
            foreach (var file in Files(Path.Combine(dir, "sessions"), 12))
            {
                bool active = false; DateTimeOffset? latest = null;
                foreach (var item in Tail(file))
                {
                    var payload = Get(item, "payload"); var type = Str(payload, "type"); var time = Date(Get(item, "timestamp"));
                    if (Str(item, "type") == "event_msg")
                    {
                        if (type is "task_started" or "user_message" or "agent_reasoning") { active = true; latest = time; }
                        if (type is "task_complete" or "turn_aborted") { active = false; latest = time; }
                        if (type == "token_count") { latest = time; }
                        var limits = Get(payload, "rate_limits");
                        if (limits.ValueKind == JsonValueKind.Object && time != null && (collected == null || time > collected))
                        {
                            five = null; week = null;
                            foreach (string key in new[] { "primary", "secondary" })
                            {
                                var window = Get(limits, key); var duration = Get(window, "window_minutes");
                                if (duration.ValueKind != JsonValueKind.Number) continue;
                                if (duration.GetInt32() == 300) five = Window(window, "used_percent", "resets_at");
                                if (duration.GetInt32() == 10080) week = Window(window, "used_percent", "resets_at");
                            }
                            account = Str(limits, "plan_type"); collected = time; source = "Codex · rate_limits da sessão local";
                        }
                    }
                    if (Str(item, "type") == "response_item" && Str(payload, "type") is "reasoning" or "custom_tool_call" or "function_call") { active = true; latest = time; }
                }
                if (active && latest > DateTimeOffset.UtcNow.AddMinutes(-3)) { working = true; activity = "Trabalhando"; }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { source = "Fonte local temporariamente indisponível"; }
        return new("Codex", account, activity, working, five, week, collected, source);
    }
}
