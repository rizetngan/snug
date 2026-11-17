using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snug.Windows.Core;

public sealed record PhoneScreen(int Width, int Height, int? Points = null)
{
    public bool IsValid => Width > 0 && Height > 0 && Width <= Height && (Points ?? 1) > 0;
    public string SizeText => $"{Width} × {Height}";

    public static PhoneScreen? Custom(int? first, int? second)
    {
        if (first is null or < 1 or > 20000 || second is null or < 1 or > 20000) return null;
        return new PhoneScreen(Math.Min(first.Value, second.Value), Math.Max(first.Value, second.Value));
    }
}

public sealed record PhoneModel(string Brand, string Name, PhoneScreen Screen)
{
    public string Id => $"{Brand} {Name}";
}

public sealed record PhoneChoice(string? Model, PhoneScreen Screen)
{
    public static PhoneChoice Standard { get; } =
        new("Apple iPhone 17 Pro Max", new PhoneScreen(1320, 2868, 440));

    public string Title => Model is null ? "自定义" : Model;
    public PhoneScreen? CustomScreen => Model is null ? Screen : null;
}

public sealed record PhoneBrand(string Name, IReadOnlyList<string> Aliases, IReadOnlyList<PhoneModel> Models);

/// <summary>从上游 Assets/phone-models.json 读取的机型库。机型数据只维护一份。</summary>
public sealed class PhoneCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<PhoneBrand> Brands { get; }
    public IReadOnlyList<PhoneModel> Models { get; }

    private PhoneCatalog(IReadOnlyList<PhoneBrand> brands)
    {
        Brands = brands;
        Models = brands.SelectMany(brand => brand.Models).ToArray();
    }

    public static PhoneCatalog FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        PhoneCatalogFile file;
        try
        {
            file = JsonSerializer.Deserialize<PhoneCatalogFile>(json, JsonOptions)
                ?? throw new PhoneCatalogException("文件内容为空");
        }
        catch (JsonException error)
        {
            throw new PhoneCatalogException($"JSON 格式错误：{error.Message}");
        }

        if (file.Brands is null || file.Brands.Count == 0)
            throw new PhoneCatalogException("没有品牌数据");

        var brandNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var brands = new List<PhoneBrand>();

        foreach (var entry in file.Brands)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) || entry.Models is null || entry.Models.Count == 0)
                throw new PhoneCatalogException($"品牌「{entry.Name}」没有名字或机型");

            if (!brandNames.Add(Normalize(entry.Name)))
                throw new PhoneCatalogException($"品牌「{entry.Name}」重复");

            var models = new List<PhoneModel>();
            foreach (var item in entry.Models)
            {
                if (string.IsNullOrWhiteSpace(item.Name) || item.Screen is not { Length: 2 })
                    throw new PhoneCatalogException($"{entry.Name} 的机型名称或 screen 数据不正确");

                var screen = new PhoneScreen(item.Screen[0], item.Screen[1], item.Points);
                if (!screen.IsValid)
                    throw new PhoneCatalogException($"{entry.Name} {item.Name}：分辨率必须为正数、竖屏短边在前");

                var model = new PhoneModel(entry.Name, item.Name, screen);
                if (!modelIds.Add(Normalize(model.Id)))
                    throw new PhoneCatalogException($"{model.Id} 重复");
                models.Add(model);
            }

            brands.Add(new PhoneBrand(entry.Name, entry.Aliases ?? [], models));
        }

        return new PhoneCatalog(brands);
    }

    public static PhoneCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }

    public PhoneModel? Find(string id) => Models.FirstOrDefault(model =>
        string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase));

    public PhoneChoice Resolve(PhoneChoice choice)
    {
        if (choice.Model is null) return choice;
        var model = Find(choice.Model);
        return model is null ? choice : new PhoneChoice(model.Id, model.Screen);
    }

    public IReadOnlyList<PhoneBrand> Search(string query)
    {
        var normalized = Normalize(query);
        if (normalized.Length == 0) return Brands;

        return Brands.Select(brand =>
        {
            var brandMatches = new[] { brand.Name }.Concat(brand.Aliases)
                .Any(value => Normalize(value).Contains(normalized, StringComparison.Ordinal));
            var models = brandMatches
                ? brand.Models
                : brand.Models.Where(model =>
                    Normalize(model.Name).Contains(normalized, StringComparison.Ordinal)
                    || Normalize(brand.Name + model.Name).Contains(normalized, StringComparison.Ordinal)).ToArray();
            return models.Count == 0 ? null : new PhoneBrand(brand.Name, brand.Aliases, models);
        }).Where(brand => brand is not null).Cast<PhoneBrand>().ToArray();
    }

    private static string Normalize(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant();

    private sealed class PhoneCatalogFile
    {
        public List<BrandEntry>? Brands { get; set; }
    }

    private sealed class BrandEntry
    {
        public string Name { get; set; } = string.Empty;
        public string[]? Aliases { get; set; }
        public List<ModelEntry>? Models { get; set; }
    }

    private sealed class ModelEntry
    {
        public string Name { get; set; } = string.Empty;
        public int[]? Screen { get; set; }
        public int? Points { get; set; }
    }
}

public sealed class PhoneCatalogException(string message) : Exception($"机型库：{message}");
