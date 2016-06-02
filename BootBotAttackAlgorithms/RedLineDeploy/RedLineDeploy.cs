using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using CoC_Bot;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using CoC_Bot.Internals;

[assembly: Addon("RedLineDeploy", "Deploy troops along the red line", "Kloc")]
namespace RedLineDeploy
{
    [AttackAlgorithm("Red Line Deploy", "Deploys troops along the red line")]
    public class RedLineDeploy : BaseAttack
    {
        public RedLineDeploy(Opponent opponent) : base(opponent)
        {
            // Default behavior
        }

        public override double ShouldAccept()
        {
            // For debugging
            // VisualizeDeployPoints();
            
            // check if the base meets the user's requirements
            if (!Opponent.MeetsRequirements(BaseRequirements.All))
            {
                Log.Debug("[Red Line] Skipping this base because it doesn't meet the requirements");
                return 0;
            }
            return 0.7;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info("[Red Line] Attack start");

            var deplyElements = Deploy.GetTroops();
            var spells = deplyElements.Extract(u => u.ElementType == DeployElementType.Spell);
            var bk = deplyElements.Extract(u => u.ElementType == DeployElementType.HeroKing);
            var aq = deplyElements.Extract(u => u.ElementType == DeployElementType.HeroQueen);
            var gw = deplyElements.Extract(u => u.ElementType == DeployElementType.HeroWarden);
            var cc = deplyElements.Extract(u => u.ElementType == DeployElementType.ClanTroops);
            var ranged = deplyElements.Extract(u => u.IsRanged);
            var units = deplyElements
                    .Where(element => element.ElementType != DeployElementType.Spell)
                    .OrderBy(element => element.UnitData?.AttackType != AttackType.Tank)
                    .ToList();

            var clockwisePoints = GameGrid.RedPoints
                .Where(
                    point =>
                        !(point.X > 18 && point.Y > 18 || point.X > 18 && point.Y < -18 || point.X < -18 && point.Y > 18 ||
                        point.X < -18 && point.Y < -18))
                .OrderBy(point => Math.Atan2(point.X, point.Y)).ToList();

            var inflatedPoints = clockwisePoints
                .Select(
                    point =>
                        new PointFT((float) (point.X + (point.X/Math.Sqrt(point.DistanceSq(new PointFT(0, 0))))*4),
                            (float) (point.Y + (point.Y/Math.Sqrt(point.DistanceSq(new PointFT(0, 0))))*4)))
                .ToList();

            while (units.Count > 0)
            {
                Log.Info("[Red Line] Deploying melee and tanking troops");
                foreach (var element in units)
                {
                    foreach (
                        var t in
                            Deploy.AtPoints(new[] {element},
                                clockwisePoints.GetNth(GameGrid.RedPoints.Length/(double) element.Count).ToArray(),
                                1))
                        yield return t;
                }
                units.Recount();
                units.RemoveAll(unit => unit.Count < 1);
            }

            while (ranged.Count > 0)
            {
                Log.Info("[Red Line] Deploying ranged troops");
                foreach (var element in ranged)
                {
                    foreach (
                        var t in
                            Deploy.AtPoints(new[] { element },
                                inflatedPoints.GetNth(GameGrid.RedPoints.Length / (double)element.Count).ToArray(),
                                1))
                        yield return t;
                }
                ranged.Recount();
                ranged.RemoveAll(unit => unit.Count < 1);
            }
            
            if (cc.Count > 0 && UserSettings.UseClanTroops)
            {
                Log.Info("[Red Line] Deploying the clan castle");
                foreach (var y in Deploy.AtPoint(cc.ToArray(), new PointFT(x: GameGrid.MinX, y: GameGrid.MinY), 1))
                    yield return y;
            }

            var heroes = new List<DeployElement>();

            if (bk.Count > 0 && UserSettings.UseKing)
            {
                heroes.AddRange(bk);
                Log.Info("[Red Line] Deploying the Barbarian King");
                foreach (var y in Deploy.AtPoint(bk.ToArray(), new PointFT(x: GameGrid.MinX, y: GameGrid.MinY), 1))
                    yield return y;
            }

            if (aq.Count > 0 && UserSettings.UseQueen)
            {
                heroes.AddRange(aq);
                Log.Info("[Red Line] Deploying the Archer Queen");
                foreach (var y in Deploy.AtPoint(aq.ToArray(), new PointFT(x: GameGrid.MinX, y: GameGrid.MinY), 1))
                    yield return y;
            }

            if (gw.Count > 0 && UserSettings.UseWarden)
            {
                heroes.AddRange(gw);
                Log.Info("[Red Line] Deploying the Grand Warden");
                foreach (var y in Deploy.AtPoint(gw.ToArray(), new PointFT(x: GameGrid.MinX, y: GameGrid.MinY), 1))
                    yield return y;
            }

            while (heroes.Count > 0 && GameStateCheck.LastKnownGameState == GameState.AttackInProgress)
            {
                TryActivateHeroAbilities(heroes);
                yield return 100;
            }

            Log.Info("[Red Line] Deploy done");
        }

        public override string ToString()
        {
            return "Red Line Deploy";
        }

        private void VisualizeDeployPoints()
        {
            var clockwisePoints = GameGrid.RedPoints.OrderBy(point => Math.Atan2(point.X, point.Y)).ToList();
            var inflatedPoints =
                clockwisePoints.Select(
                    point =>
                        new PointFT((float)(point.X + (point.X / Math.Sqrt(point.DistanceSq(new PointFT(0, 0)))) * 4),
                            (float)(point.Y + (point.Y / Math.Sqrt(point.DistanceSq(new PointFT(0, 0)))) * 4)))
                    .ToList();

            using (Bitmap bmp = Core.GetFrameCopy())
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    foreach (PointFT redPoint in clockwisePoints)
                        Visualize.RectangleT(bmp, new RectangleT((int)redPoint.X, (int)redPoint.Y, 1, 1), new Pen(Color.FromArgb(128, Color.DarkRed)));

                    foreach (PointFT redPoint in inflatedPoints)
                        Visualize.RectangleT(bmp, new RectangleT((int)redPoint.X, (int)redPoint.Y, 1, 1), new Pen(Color.FromArgb(128, Color.DarkBlue)));
                }
                var d = DateTime.UtcNow;
                DebugTools.SaveDebugScreenshot($"RedLineDeploy {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}", bmp);
            }
        }
    }

    public static class ListExtension
    {
        public static IEnumerable<T> GetNth<T>(this List<T> list, double n)
        {
            for (double i = 0; i < list.Count; i += n)
                yield return list[(int)i];
        }
    }
}
