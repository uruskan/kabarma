namespace Kabarma.Sim;

// server.json: adanmis sunucu (TEKNIK §5, §12; M5 sunucu akisi). Godot katmani game/scripts/server/.

public sealed class ServerPingDef
{
    public string ListFile { get; set; } = "user://servers.json";
    public int SteamMaxResults { get; set; } = 200;
    public float SteamTimeoutSeconds { get; set; } = 6f;
}

public sealed class ServerSteamDef
{
    public bool Listed { get; set; } = true;
    public string GameDir { get; set; } = "kabarma";
    public string Product { get; set; } = "kabarma";
    public string Description { get; set; } = "Kabarma";
}

public sealed class ServerFile
{
    public string Name { get; set; } = "Kabarma Sunucusu";
    public string Motd { get; set; } = "";
    public int Port { get; set; } = 27015;
    public int MaxPlayers { get; set; } = 8;
    public string Password { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    public string SaveSlot { get; set; } = "sunucu";
    public float AutosaveMinutes { get; set; } = 5f;
    public string StateFile { get; set; } = "server_status.txt";
    public string CommandFile { get; set; } = "server_commands.txt";
    public string ResultFile { get; set; } = "server_results.log";
    public string RuntimeFile { get; set; } = "server_runtime.json";
    public bool Stdin { get; set; } = true;
    public ServerPingDef Ping { get; set; } = new();
    public ServerSteamDef Steam { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("server.json")]
    public ServerFile Server { get; private set; } = new();
}
