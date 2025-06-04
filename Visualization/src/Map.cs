using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace mmvp.src;


public class Map(List<List<Map.Field>> data)
{
    private static TileSetCoordinates GetScribbleDungeonTiles()
    {

        return new TileSetCoordinates(
            SourceId: 3,
            Wall: new(new(5, 5), 1),
            VerticalWall: new(new(5, 5)),
            LeftUpperWallCorner: new(new(5, 4)),
            RightUpperWallCorner: new(new(9, 4)),
            LeftLowerWallCorner: new(new(9, 4)),
            RightLowerWallCorner: new(new(9, 4)),
            Floor: new(new(0, 0)),
            Hill: new(new(5, 0)),
            Ditch: new(new(0, 0), 1),
            Water: new(new(0, 0), 2),
            ExplosiveBarrel: new(new(5, 2)),
            FlagStand1: new(new(1, 8)),
            FlagStand2: new(new(3, 8))
        );
    }

    private static TileSetCoordinates GetMinipackTiles()
    {

        return new TileSetCoordinates(
            SourceId: 0,
            Wall: new(new(0, 1), 1),
            VerticalWall: new(new(0, 1)),
            LeftUpperWallCorner: new(new(0, 0)),
            RightUpperWallCorner: new(new(9, 4)),
            LeftLowerWallCorner: new(new(9, 4)),
            RightLowerWallCorner: new(new(9, 4)),
            Floor: new(new(4, 2)),
            Hill: new(new(5, 0)),
            Ditch: new(new(4, 2), 1),
            Water: new(new(0, 3)),
            ExplosiveBarrel: new(new(3, 2)),
            FlagStand1: new(new(1, 8)),
            FlagStand2: new(new(3, 8))
        );
    }

    private static TileSetCoordinates GetTopDownShooterTiles()
    {

        return new TileSetCoordinates(
            SourceId: 2,
            Wall: new(new(11, 4)),
            VerticalWall: new(new(11, 5)),
            LeftUpperWallCorner: new(new(9, 4)),
            RightUpperWallCorner: new(new(10, 4)),
            LeftLowerWallCorner: new(new(9, 5)),
            RightLowerWallCorner: new(new(10, 5)),
            Floor: new(new(22, 14)),
            Hill: new(new(26, 18)),
            Ditch: new(new(26, 17)),
            Water: new(new(25, 18)),
            ExplosiveBarrel: new(new(25, 16)),
            FlagStand1: new(new(26, 6)),
            FlagStand2: new(new(26, 5))
        );
    }

    private record TileSetCoordinates(
        int SourceId,
        TileMapCell Wall,
        TileMapCell VerticalWall,
        TileMapCell LeftUpperWallCorner,
        TileMapCell RightUpperWallCorner,
        TileMapCell LeftLowerWallCorner,
        TileMapCell RightLowerWallCorner,
        TileMapCell Floor,
        TileMapCell Hill,
        TileMapCell Ditch,
        TileMapCell Water,
        TileMapCell ExplosiveBarrel,
        TileMapCell FlagStand1,
        TileMapCell FlagStand2);

    private record TileMapCell(Vector2I AtlasCoords, int AlternativeTile = 0);

    public enum Field
    {
        Floor,
        Wall,
        Hill,
        Ditch,
        Water,
        ExplosiveBarrel,
        FlagStand1,
        FlagStand2,
    }

    private TileSetCoordinates currentTileSetCoords = GetMinipackTiles();

    public static Map ReadInMap()
    {
        var mapPath = GetMapFilename();
        var mapData = new List<List<Field>>();
        string[] lines = File.ReadAllLines(mapPath);
        foreach (string line in lines)
        {
            var row = line.Split(';').Select(field => field switch
            {
                "0" => Field.Floor,
                "1" => Field.Wall,
                "2" => Field.Hill,
                "3" => Field.Ditch,
                "4" => Field.Water,
                "5" => Field.ExplosiveBarrel,
                "7" => Field.FlagStand1,
                "8" => Field.FlagStand2,
                _ => throw new ArgumentException("Encountered an unknown map field."),
            }).ToList();
            mapData.Add(row);
        }

        return new Map(mapData);
    }

    private static string GetMapFilename()
    {
        string configFilename = "config.json";
        var cwd = Directory.GetCurrentDirectory().Split("/").Last();
        GD.Print("CWD: ", cwd);
        var pathToLasertagBox = cwd switch
        {
            "LaserTagBox" => "./",
            "model-mars-laser-tag-game" => "./LaserTagBox/",
            "Visualization" => "../LaserTagBox/",
            _ => throw new NotImplementedException()
        };
        string pathToConfigFile = $"{pathToLasertagBox}{configFilename}";

        if (!File.Exists(pathToConfigFile))
        {
            throw new FileNotFoundException($"Config file {pathToConfigFile} does not exist");
        }

        var configJson = File.ReadAllText(pathToConfigFile);
        var config = JsonSerializer.Deserialize<LaserTagConfig>(configJson);

        var playerBodyLayer = config.Layers.Find(l => l.Name == "PlayerBodyLayer");
        return $"{pathToLasertagBox}{playerBodyLayer.File}";
    }

    public override string ToString()
    {
        return string.Join("", data.ConvertAll(row => string.Join("", row.ConvertAll(field => field switch
        {
            Field.Floor => " ",
            Field.Wall => "H",
            Field.Hill => "^",
            Field.Ditch => "_",
            _ => "="
        })) + "\n"));
    }

    public Vector2I Size()
    {
        return new(data[0].Count, data.Count);
    }

    public void PopulateTileMap(TileMapLayer tileMapLayer)
    {
        currentTileSetCoords = tileMapLayer.Name.ToString() switch
        {
            "BaseMap" => GetScribbleDungeonTiles(),
            "MinipackBaseMap" => GetMinipackTiles(),
            "TopDownShooterBaseMap" => GetTopDownShooterTiles(),
            var other => throw new NotImplementedException($"No TileSet for: {other}"),
        };
        for (int y = 0; y < data.Count; ++y)
        {
            for (int x = 0; x < data[y].Count; ++x)
            {
                var fieldValue = data[y][x];
                var tileSetField = fieldValue switch
                {
                    Field.Floor => currentTileSetCoords.Floor,
                    Field.Wall => GetWallPosition(x, y) switch
                    {
                        WallPosition.Horizontal => currentTileSetCoords.Wall,
                        WallPosition.Vertical => currentTileSetCoords.VerticalWall,
                        WallPosition.LeftUpperCorner => currentTileSetCoords.LeftUpperWallCorner,
                        WallPosition.RightUpperCorner => currentTileSetCoords.RightUpperWallCorner,
                        WallPosition.LeftLowerCorner => currentTileSetCoords.LeftLowerWallCorner,
                        WallPosition.RightLowerCorner => currentTileSetCoords.RightLowerWallCorner,
                        _ => throw new UnreachableException(),
                    },
                    Field.Hill => currentTileSetCoords.Hill,
                    Field.Ditch => currentTileSetCoords.Ditch,
                    Field.Water => currentTileSetCoords.Water,
                    Field.ExplosiveBarrel => currentTileSetCoords.ExplosiveBarrel,
                    Field.FlagStand1 => currentTileSetCoords.FlagStand1,
                    Field.FlagStand2 => currentTileSetCoords.FlagStand2,
                    _ => throw new NotImplementedException(),
                };
                tileMapLayer.SetCell(new Vector2I(x, y), currentTileSetCoords.SourceId, tileSetField.AtlasCoords, tileSetField.AlternativeTile);
            }
        }
    }

    private enum WallPosition
    {
        Horizontal,
        Vertical,
        LeftUpperCorner,
        RightUpperCorner,
        LeftLowerCorner,
        RightLowerCorner,
    }

    private WallPosition GetWallPosition(int x, int y)
    {
        bool hasNorth = HasWall(x, y - 1);
        bool hasSouth = HasWall(x, y + 1);
        bool hasEast = HasWall(x + 1, y);
        bool hasWest = HasWall(x - 1, y);

        int wallCount = (hasNorth ? 1 : 0) + (hasSouth ? 1 : 0) +
                        (hasEast ? 1 : 0) + (hasWest ? 1 : 0);

        return wallCount switch
        {
            1 => WallPosition.Horizontal,
            2 when hasNorth && hasSouth => WallPosition.Vertical,
            2 when hasSouth && hasEast => WallPosition.LeftUpperCorner,
            2 when hasSouth && hasWest => WallPosition.RightUpperCorner,
            2 when hasNorth && hasEast => WallPosition.LeftLowerCorner,
            2 when hasNorth && hasWest => WallPosition.RightLowerCorner,
            _ => WallPosition.Horizontal,

        };
    }

    private bool HasWall(int x, int y)
    {
        // handle out of bounds
        if (y < 0 || y >= data.Count || x < 0 || x >= data[y].Count)
            return false;

        return data[y][x] is Field.Wall;
    }
}

public class LaserTagConfig
{
    [JsonPropertyName("layers")]
    public List<LayerConfig> Layers { get; set; } = [];
}

public class LayerConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("file")]
    public string File { get; set; } = "";
}
