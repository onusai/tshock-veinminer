using System;
using System.Collections.Generic;
using System.Linq;
using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using Microsoft.Xna.Framework;
using System.Threading;
using System.Text.Json;

namespace VeinMiner
{
    [ApiVersion(2, 1)]
    public class VeinMiner : TerrariaPlugin
    {
        public Thread delayThread;
        public bool active = true;

        public List<List<Vector2>> ActiveVeins = new List<List<Vector2>>();
        private Object tLock = new Object();

        public class ConfigData
        {
            public bool PluginEnabled { get; set; } = true;
            public int SearchLimit { get; set; } = 200;
            public int BreakLimit { get; set; } = 4;
            public int[] TileIds { get; set; } = new int[] { 7, 166, 6, 167, 9, 168, 8, 169, 407, 37, 22,
            204, 58, 107, 221, 108, 222, 111, 223, 211, 408, 63, 64, 65, 66, 67, 68, 404 };
        }

        ConfigData configData;

        public override string Author => "Onusai";
        public override string Description => "Mine ores with ease";
        public override string Name => "VeinMiner";
        public override Version Version => new Version(1, 0, 0, 0);

        public VeinMiner(Main game) : base(game) { }

        public override void Initialize()
        {
            configData = PluginConfig.Load("VeinMiner");
            ServerApi.Hooks.GameInitialize.Register(this, OnGameLoad);
        }

        void OnGameLoad(EventArgs e)
        {
            if (!configData.PluginEnabled) return;

            delayThread = new Thread(new ThreadStart(DelayMine));
            delayThread.Start();

            TShockAPI.GetDataHandlers.TileEdit += TileEdit;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                active = false;
                if (configData.PluginEnabled) delayThread.Join();
                ServerApi.Hooks.GameInitialize.Deregister(this, OnGameLoad);
                if (configData.PluginEnabled) TShockAPI.GetDataHandlers.TileEdit -= TileEdit;
            }
            base.Dispose(disposing);
        }

        void ScanVein(int id, int x, int y, List<Vector2> coords)
        {
            if (x > Main.maxTilesX || x < 0) return;
            if (y > Main.maxTilesY || y < 0) return;
            if (Main.tile[x, y].type != id) return;
            if (coords.Count >= configData.SearchLimit) return;

            var pos = new Vector2(x, y);

            if (coords.Contains(pos)) return;
            coords.Add(pos);

            ScanVein(id, x, y + 1, coords);
            ScanVein(id, x, y - 1, coords);
            ScanVein(id, x + 1, y, coords);
            ScanVein(id, x - 1, y, coords);
            ScanVein(id, x + 1, y + 1, coords);
            ScanVein(id, x - 1, y - 1, coords);
            ScanVein(id, x + 1, y - 1, coords);
            ScanVein(id, x - 1, y + 1, coords);
        }

        void TileEdit(object sender, TShockAPI.GetDataHandlers.TileEditEventArgs args)
        {

            if (args.Action.ToString().Equals("KillTile") &&
                    args.EditData == 0 &&
                    configData.TileIds.Contains(Main.tile[args.X, args.Y].type))
            {

                var coords = new List<Vector2>();
                ScanVein(Main.tile[args.X, args.Y].type, args.X, args.Y, coords);

                var origin = new Vector2(args.X, args.Y);
                coords = coords.OrderBy(pos => Vector2.Distance(origin, pos)).ToList();

                if (coords.Count() < configData.BreakLimit)
                    coords = coords.GetRange(0, coords.Count());
                else
                    coords = coords.GetRange(0, configData.BreakLimit);

                lock (tLock)
                {
                    ActiveVeins.Add(coords);
                }
            }
        }

        void DelayMine()
        {
            while (active)
            {
                lock (tLock)
                {
                    for (int i = ActiveVeins.Count - 1; i >= 0; i--)
                    {
                        var list = ActiveVeins[i];

                        if (list.Count == 0) ActiveVeins.RemoveAt(i);
                        else
                        {
                            int x = (int)list[0].X;
                            int y = (int)list[0].Y;
                            list.RemoveAt(0);

                            if (WorldGen.CanKillTile(x, y))
                            {
                                NetMessage.SendData((int)PacketTypes.Tile, -1, -1, Terraria.Localization.NetworkText.Empty, 0, x, y);
                                WorldGen.KillTile(x, y);
                            }
                        }
                    }
                }
                Thread.Sleep(50);
            }
        }

        public static class PluginConfig
        {
            public static string filePath;
            public static ConfigData Load(string Name)
            {
                filePath = String.Format("{0}/{1}.json", TShock.SavePath, Name);

                if (!File.Exists(filePath))
                {
                    var data = new ConfigData();
                    Save(data);
                    return data;
                }

                var jsonString = File.ReadAllText(filePath);
                var myObject = JsonSerializer.Deserialize<ConfigData>(jsonString);

                return myObject;
            }

            public static void Save(ConfigData myObject)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var jsonString = JsonSerializer.Serialize(myObject, options);

                File.WriteAllText(filePath, jsonString);
            }
        }
    }
}
