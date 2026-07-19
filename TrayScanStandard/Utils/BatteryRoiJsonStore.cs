using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TrayScanStandard.Data;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Models;

namespace TrayScanStandard.Utils
{
    public static class BatteryRoiJsonStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        private static string ConfigDirectoryPath => Path.Combine(AppContext.BaseDirectory, "Config");
        private static string ConfigFilePath => Path.Combine(ConfigDirectoryPath, "BatteryRoiConfig.json");

        public static void SaveBattery(BatteryTypeInfo battery)
        {
            if (battery is null || string.IsNullOrWhiteSpace(battery.TypeName))
            {
                return;
            }

            var config = LoadConfig() ?? new BatteryRoiConfig();
            var snapshot = BatteryRoiSnapshot.FromBattery(battery);

            // 优先按 BatteryId 匹配，TypeName 作为备选
            var index = config.Batteries.FindIndex(s =>
                (s.BatteryId > 0 && s.BatteryId == battery.Id)
                || string.Equals(s.TypeName, battery.TypeName, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                config.Batteries[index] = snapshot;
            }
            else
            {
                config.Batteries.Add(snapshot);
            }

            Directory.CreateDirectory(ConfigDirectoryPath);
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config, SerializerOptions));
        }

        public static void SyncToDatabase(LinxContext context)
        {
            var config = LoadConfig();
            if (config?.Batteries is null || config.Batteries.Count == 0)
            {
                return;
            }

            var batteries = context.BatteryTypeInfos.ToList();
            var hasChanges = false;

            foreach (var battery in batteries)
            {
                // 优先按 BatteryId 匹配（精确），TypeName 作为备选（不同电池类型可能重名）
                var snapshot = config.Batteries.FirstOrDefault(s => s.BatteryId > 0 && s.BatteryId == battery.Id)
                    ?? config.Batteries.FirstOrDefault(s =>
                        string.Equals(s.TypeName, battery.TypeName, StringComparison.OrdinalIgnoreCase));

                if (snapshot is null)
                {
                    continue;
                }

                var regions = CloneRegions(snapshot.Regions);
                if (RegionsEqual(battery.Regions, regions))
                {
                    continue;
                }

                battery.Regions = regions;
                hasChanges = true;
            }

            if (hasChanges)
            {
                context.SaveChanges();
            }
        }

        private static BatteryRoiConfig? LoadConfig()
        {
            if (!File.Exists(ConfigFilePath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(ConfigFilePath);
                return JsonSerializer.Deserialize<BatteryRoiConfig>(json, SerializerOptions);
            }
            catch
            {
                return null;
            }
        }

        private static bool RegionsEqual(List<List<BarCodeRegionInfo>>? left, List<List<BarCodeRegionInfo>>? right)
        {
            left ??= [];
            right ??= [];

            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                var leftRegions = left[i] ?? [];
                var rightRegions = right[i] ?? [];

                if (leftRegions.Count != rightRegions.Count)
                {
                    return false;
                }

                for (int j = 0; j < leftRegions.Count; j++)
                {
                    var l = leftRegions[j];
                    var r = rightRegions[j];
                    if (l.Top != r.Top
                        || l.Left != r.Left
                        || l.Width != r.Width
                        || l.Height != r.Height
                        || l.ChannelIdx != r.ChannelIdx)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static List<List<BarCodeRegionInfo>> CloneRegions(List<List<BarCodeRegionInfo>>? regions)
        {
            regions ??= [];
            return regions
                .Select(regionList => (regionList ?? [])
                    .Select(region => new BarCodeRegionInfo
                    {
                        Top = region.Top,
                        Left = region.Left,
                        Width = region.Width,
                        Height = region.Height,
                        ChannelIdx = region.ChannelIdx
                    })
                    .ToList())
                .ToList();
        }

        private sealed class BatteryRoiConfig
        {
            public List<BatteryRoiSnapshot> Batteries { get; set; } = [];
        }

        private sealed class BatteryRoiSnapshot
        {
            public int BatteryId { get; set; }
            public string TypeName { get; set; } = string.Empty;
            public List<List<BarCodeRegionInfo>> Regions { get; set; } = [];

            public static BatteryRoiSnapshot FromBattery(BatteryTypeInfo battery)
            {
                return new BatteryRoiSnapshot
                {
                    BatteryId = battery.Id,
                    TypeName = battery.TypeName,
                    Regions = CloneRegions(battery.Regions)
                };
            }
        }
    }
}
