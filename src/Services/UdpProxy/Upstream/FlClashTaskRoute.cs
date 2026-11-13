using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

namespace Game_Daily_Routine_Launcher;

public sealed record FlClashTaskRoute(string NodeName, string Configuration);

public static class FlClashTaskRouteBuilder
{
    public static JsonObject ParseYaml(string text)
    {
        var parsed = new DeserializerBuilder().WithDuplicateKeyChecking()
            .WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object>(text);
        return JsonSerializer.SerializeToNode(Normalize(parsed, 0))!.AsObject();
    }

    private static object? Normalize(object? value, int depth)
    {
        if (depth > 50) throw new InvalidOperationException("代理配置嵌套过深。");
        return value switch
        {
            IDictionary<object, object> map => map.ToDictionary(pair => pair.Key.ToString()!,
                pair => Normalize(pair.Value, depth + 1)),
            IList<object> list => list.Select(item => Normalize(item, depth + 1)).ToArray(),
            _ => value
        };
    }

    public static FlClashTaskRoute Build(JsonObject source, JsonObject preferences, int port)
    {
        var profileId = preferences["currentProfileId"]?.ToString();
        var profile = (preferences["profiles"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(item => item["id"]?.ToString() == profileId)
            ?? throw new InvalidOperationException("无法识别 FlClash 当前订阅。");
        var selected = profile["selectedMap"] as JsonObject ?? new();
        var groups = Index(source["proxy-groups"]);
        var proxies = Index(source["proxies"]);
        var root = (source["rules"] as JsonArray)?.Select(item => item?.GetValue<string>() ?? "")
            .LastOrDefault(rule => rule.StartsWith("MATCH,", StringComparison.OrdinalIgnoreCase))?.Split(',')[1];
        if (source["mode"]?.ToString() == "global") root = selected["GLOBAL"]?.ToString() ?? root;
        root ??= selected.FirstOrDefault().Key;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var name = root ?? string.Empty;
        while (groups.TryGetValue(name, out var group))
        {
            if (!visited.Add(name)) throw new InvalidOperationException("FlClash 代理组存在循环引用。");
            var candidates = (group["proxies"] as JsonArray)?.Select(item => item!.ToString()).ToArray() ?? [];
            var choice = selected[name]?.ToString();
            if (choice is not null && !candidates.Contains(choice))
                throw new InvalidOperationException("FlClash 当前选择不在已缓存的代理组中，请刷新订阅后重试。");
            name = choice ?? candidates.FirstOrDefault() ?? "";
        }
        if (!proxies.TryGetValue(name, out var node))
            throw new InvalidOperationException("FlClash 当前出口是直连或尚未缓存的订阅节点，无法为任务建立独立代理出口。");
        if (node["udp"]?.ToString().Equals("false", StringComparison.OrdinalIgnoreCase) == true)
            throw new InvalidOperationException("FlClash 当前选中的节点明确禁用了 UDP。");
        var included = new JsonArray();
        var copied = new HashSet<string>(StringComparer.Ordinal);
        var copying = new HashSet<string>(StringComparer.Ordinal);
        void CopyNode(string key)
        {
            if (copied.Contains(key)) return;
            if (!copying.Add(key)) throw new InvalidOperationException("当前节点的代理链存在循环引用。");
            if (!proxies.TryGetValue(key, out var current))
                throw new InvalidOperationException("当前节点依赖未缓存的代理链，无法独立启动。");
            var copy = (JsonObject)current.DeepClone();
            if (copy["dialer-proxy"] is JsonValue dialer) CopyNode(dialer.ToString());
            if (key == name) copy["name"] = "MAAA-selected";
            included.Add(copy);
            copying.Remove(key);
            copied.Add(key);
        }
        CopyNode(name);
        var isolated = new JsonObject
        {
            ["mixed-port"] = port, ["bind-address"] = "127.0.0.1", ["allow-lan"] = false,
            ["mode"] = "rule", ["log-level"] = "error", ["ipv6"] = true,
            ["tun"] = new JsonObject { ["enable"] = false },
            ["dns"] = new JsonObject { ["enable"] = false },
            ["profile"] = new JsonObject { ["store-selected"] = false, ["store-fake-ip"] = false },
            ["proxies"] = included, ["rules"] = new JsonArray("MATCH,MAAA-selected")
        };
        return new(name, isolated.ToJsonString());
    }

    private static Dictionary<string, JsonObject> Index(JsonNode? value) =>
        (value as JsonArray ?? []).OfType<JsonObject>().ToDictionary(item => item["name"]!.ToString(),
            StringComparer.Ordinal);
}
