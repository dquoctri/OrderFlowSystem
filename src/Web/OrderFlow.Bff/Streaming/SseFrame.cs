using System.Runtime.CompilerServices;

namespace OrderFlow.Bff.Streaming;

public sealed record SseFrame(string? Id, string Event, string Data)
{
    public string Encode() => (Id is null ? "" : $"id: {Id}\n") + $"event: {Event}\n" +
        string.Concat(Data.Split('\n').Select(line => $"data: {line}\n")) + "\n";

    public static async IAsyncEnumerable<SseFrame> ReadAsync(Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        string? id = null;
        var eventName = "message";
        var data = new List<string>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Count > 0) yield return new SseFrame(id, eventName, string.Join('\n', data));
                id = null;
                eventName = "message";
                data.Clear();
                continue;
            }
            if (line[0] == ':') continue;
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            var field = separator < 0 ? line : line[..separator];
            var value = separator < 0 ? "" : line[(separator + 1)..];
            if (value.StartsWith(' ')) value = value[1..];
            switch (field)
            {
                case "id": if (!value.Contains('\0')) id = value; break;
                case "event": eventName = value; break;
                case "data": data.Add(value); break;
            }
        }
    }
}
