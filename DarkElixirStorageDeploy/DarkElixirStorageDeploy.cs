using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CoC_Bot;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;

[assembly: Addon("DarkElixirStorageDeploy", "Target the dark elixir storage.", "Kloc")]
namespace DarkElixirStorageDeploy
{
    [AttackAlgorithm("Dark Elixir Storage Deploy", "Target the dark elixir storage.")]
    public class DarkElixirStorageDeploy : BaseAttack
    {
        public DarkElixirStorageDeploy(Opponent opponent) : base(opponent)
        {
            // Default behavior
        }

        public override string ToString()
        {
            return "Dark Elixir Storage Deploy";
        }

        public override double ShouldAccept()
        {
            // For Debugging
            // CreateDeployLines();
            VisualizeDeployment();

            return 0;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            yield break;
        }

        private static void VisualizeDeployment()
        {
            var walls = Wall.Find();
            var darkElixirStorage = DarkElixirStorage.Find().FirstOrDefault();

            using (var bmp = Screenshot.Capture())
            {
                AutoRecycle.Remove(bmp);
                Visualize.Grid(bmp);
                Visualize.Axes(bmp);
                foreach (var wall in walls) Visualize.RectangleT(bmp, wall.Location, new Pen(Color.FromArgb(0xC0, 0x00, 0xFF, 0xFF), 1));
                if (darkElixirStorage != null) Visualize.RectangleT(bmp, darkElixirStorage.Location, new Pen(Color.FromArgb(192, 85, 0, 255), 1));
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"DarkElixirStorageDeploy {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }
    }
}
