using CoC_Bot.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CoC_Bot;
using CoC_Bot.API.Buildings;
using System.Drawing;
using CoC_Bot.Internals.Geometry;
using CoC_Bot.Modules.AttackAlgorithms;

[assembly: Addon("Breakthrough Deploy", "One side GiBarch deployment for TH11 farming.", "Todd Skelton")]
namespace BreakthroughDeploy
{
    [AttackAlgorithm("Breakthrough Deploy", "One side GiBarch deployment for TH11 farming.")]
    public class BreakthroughDeploy : BaseAttack
    {
        public BreakthroughDeploy(Opponent opponent) : base(opponent)
        {
            // Default behavior
        }

        public override string ToString()
        {
            return "Breakthrough Deploy";
        }

        public override double ShouldAccept()
        {
            // For Debugging
            // CreateDeployLines();
            // VisualizeDeployment();

            return Opponent.MeetsRequirements(BaseRequirements.All) ? 1 : 0;
        }

        private PointFT _orgin, _healPoint, _ragePoint;

        private Tuple<PointFT, PointFT> _tankLine;

        private readonly List<Tuple<PointFT, PointFT>> _meleeLines = new List<Tuple<PointFT, PointFT>>();

        private readonly List<Tuple<PointFT, PointFT>> _rangedLines = new List<Tuple<PointFT, PointFT>>();

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info("[Deploy] Deploy start");
            Log.Debug("[Deploy] Start troop scan");

            CreateDeployLines();

            // get a list of all deployable units
            var deployElements = Deploy.GetTroops();

            // extract spells into their own list
            var spells = deployElements.Extract(element => element.ElementType == DeployElementType.Spell).ToArray();

            // extract heores into their own list
            var heroes = deployElements.Extract(element => element.IsHero).ToArray();

            // extract clanCastle into its own list
            var clanCastle = deployElements.Extract(element => element.ElementType == DeployElementType.ClanTroops).ToArray();

            // get tanks
            var tanks = deployElements.GetByAttackType(AttackType.Tank).ToArray();

            // get wallbreakers
            var wallBreakers = deployElements.GetByAttackType(AttackType.Wallbreak).ToArray();

            // get healers
            var healers = deployElements.GetByAttackType(AttackType.Heal).ToArray();

            // get melee
            var melee = deployElements.GetByAttackType(AttackType.Damage).Where(unit => !unit.IsRanged).ToArray();

            // get ranged
            var ranged = deployElements.GetByAttackType(AttackType.Damage).Where(unit => unit.IsRanged).ToArray();

            // get heal spells
            var healSpells = spells.Get(DeployElementName.HealSpell).ToArray();

            // get rage spells
            var rageSpells = spells.Get(DeployElementName.RageSpell).ToArray();

            // user's wave delay setting
            var waveDelay = (int)(UserSettings.WaveDelay * 1000);

            if (tanks.Any())
            {
                Log.Debug("[Deploy] Deploying tanks");
                foreach (var t in Deploy.AlongLine(tanks, _tankLine.Item1, _tankLine.Item2, 2))
                    yield return t;
                yield return waveDelay;
            }

            if (wallBreakers.Any())
            {
                Log.Debug("[Deploy] Deploying wallbreakers");
                foreach (var t in Deploy.AtPoint(wallBreakers, _orgin))
                    yield return t;
                yield return waveDelay;
            }

            if (healers.Any())
            {
                Log.Debug("[Deploy] Deploying healers");
                foreach (var t in Deploy.AlongLine(healers, _tankLine.Item1, _tankLine.Item2, 2))
                    yield return t;
                yield return waveDelay;
            }

            if (melee.Any())
            {
                Log.Debug("[Deploy] Deploying melee troops");
                foreach (var t in Deploy.InWaves(melee, _meleeLines.ToArray(), UserSettings.WaveSize))
                    yield return t;
                yield return waveDelay;
            }

            if (ranged.Any())
            {
                Log.Debug("[Deploy] Deploying ranged troops");
                foreach (var t in Deploy.InWaves(ranged, _rangedLines.ToArray(), UserSettings.WaveSize))
                    yield return t;
                yield return waveDelay;
            }

            if (healSpells.Any())
            {
                Log.Debug("[Deploy] Deploying heal");
                foreach (var t in Deploy.AtPoint(healSpells, _healPoint, 1))
                    yield return t;
                yield return waveDelay;
            }

            if (rageSpells.Any())
            {
                Log.Debug("[Deploy] Deploying rage");
                foreach (var t in Deploy.AtPoint(rageSpells, _ragePoint, 1))
                    yield return t;
                yield return waveDelay;
            }

            if (UserSettings.UseClanTroops && clanCastle.Any())
            {
                Log.Debug("[Deploy] Deploying clan castle");
                foreach (var t in Deploy.AtPoint(clanCastle, _orgin))
                    yield return t;
                yield return waveDelay;
            }

            if (heroes.Any())
            {
                Log.Debug("[Deploy] Deploying heroes");
                foreach (var t in Deploy.AtPoint(heroes, _orgin))
                    yield return t;
                yield return waveDelay;
            }

            Deploy.WatchHeroes(heroes.ToList());
        }


        private void CreateDeployLines()
        {
            var target = DarkElixirStorage.Find().FirstOrDefault()?.Location.GetCenter() ??
                         TownHall.Find()?.Location.GetCenter() ?? new PointFT(0, 0);

            var orginPoints = new[]
            {
                new PointFT(GameGrid.MaxX, 0),
                new PointFT(GameGrid.MinX, 0),
                new PointFT(0, GameGrid.MaxY),
                new PointFT(0, GameGrid.MinY)
            };

            _orgin = orginPoints.OrderBy(point => point.DistanceSq(target)).First();

            if (_orgin.X > 0)
            {
                var redLinePoint = GameGrid.RedPoints.Where(point => point.Y > -10 && point.Y < 10)
                    .Max(point => point.X);

                var adjustedRedPoints =
                    GameGrid.RedPoints.Where(point => point.Y > GameGrid.MinY + 5 && point.Y < GameGrid.MaxY - 5)
                        .ToList();

                // get the corner deployment point based on the largest xy in the quadrant.
                var posCornerPoint = adjustedRedPoints.OrderByDescending(point => point.X + point.Y).First();

                var negCornerPoint = adjustedRedPoints.OrderByDescending(point => point.X - point.Y).First();

                _healPoint = new PointFT(redLinePoint - 9, 0);
                _ragePoint = new PointFT(redLinePoint - 12, 0);

                var t1A = new PointFT(redLinePoint, GameGrid.MaxY - 10);
                var t1B = new PointFT(redLinePoint, GameGrid.MinY + 10);
                _tankLine = new Tuple<PointFT, PointFT>(t1A, t1B);

                var m1A = posCornerPoint;
                var m1B = new PointFT(redLinePoint + 5, 0);
                _meleeLines.Add(new Tuple<PointFT, PointFT>(m1A, m1B));

                var m2A = negCornerPoint;
                var m2B = new PointFT(redLinePoint + 5, 0);
                _meleeLines.Add(new Tuple<PointFT, PointFT>(m2A, m2B));

                var r1A = new PointFT(posCornerPoint.X + 2, posCornerPoint.Y);
                var r1B = new PointFT(redLinePoint + 7, 0);
                _rangedLines.Add(new Tuple<PointFT, PointFT>(r1A, r1B));

                var r2A = new PointFT(negCornerPoint.X + 2, negCornerPoint.Y);
                var r2B = new PointFT(redLinePoint + 7, 0);
                _rangedLines.Add(new Tuple<PointFT, PointFT>(r2A, r2B));
            }
            else if (_orgin.X < 0)
            {
                var redLinePoint = GameGrid.RedPoints.Where(point => point.Y > -10 && point.Y < 10)
                    .Min(point => point.X);

                var adjustedRedPoints =
                    GameGrid.RedPoints.Where(point => point.Y > GameGrid.MinY + 5 && point.Y < GameGrid.MaxY - 5)
                        .ToList();

                // get the corner deployment point based on the largest xy in the quadrant.
                var posCornerPoint = adjustedRedPoints.OrderByDescending(point => -point.X + point.Y).First();

                var negCornerPoint = adjustedRedPoints.OrderByDescending(point => -point.X - point.Y).First();

                _healPoint = new PointFT(redLinePoint + 9, 0);
                _ragePoint = new PointFT(redLinePoint + 12, 0);

                var t1A = new PointFT(redLinePoint, GameGrid.MaxY - 10);
                var t1B = new PointFT(redLinePoint, GameGrid.MinY + 10);
                _tankLine = new Tuple<PointFT, PointFT>(t1A, t1B);

                var m1A = posCornerPoint;
                var m1B = new PointFT(redLinePoint - 5, 0);
                _meleeLines.Add(new Tuple<PointFT, PointFT>(m1A, m1B));

                var m2A = negCornerPoint;
                var m2B = new PointFT(redLinePoint - 5, 0);
                _meleeLines.Add(new Tuple<PointFT, PointFT>(m2A, m2B));

                var r1A = new PointFT(posCornerPoint.X - 2, posCornerPoint.Y);
                var r1B = new PointFT(redLinePoint - 7, 0);
                _rangedLines.Add(new Tuple<PointFT, PointFT>(r1A, r1B));

                var r2A = new PointFT(negCornerPoint.X - 2, negCornerPoint.Y);
                var r2B = new PointFT(redLinePoint - 7, 0);
                _rangedLines.Add(new Tuple<PointFT, PointFT>(r2A, r2B));
            }
            else if (_orgin.Y > 0)
            {
                var redLinePoint = GameGrid.RedPoints.Where(point => point.X > -10 && point.X < 10)
                    .Max(point => point.Y);

                var adjustedRedPoints =
                    GameGrid.RedPoints.Where(point => point.X > GameGrid.MinX + 5 && point.X < GameGrid.MaxX - 5)
                        .ToList();

                // get the corner deployment point based on the largest xy in the quadrant.
                var posCornerPoint = adjustedRedPoints.OrderByDescending(point => point.X + point.Y).First();

                var negCornerPoint = adjustedRedPoints.OrderByDescending(point => -point.X + point.Y).First();

                _healPoint = new PointFT(0, redLinePoint - 9);
                _ragePoint = new PointFT(0, redLinePoint - 12);

                var t1A = new PointFT(GameGrid.MaxX - 10, redLinePoint);
                var t1B = new PointFT(GameGrid.MinX + 10, redLinePoint);
                _tankLine = new Tuple<PointFT, PointFT>(t1A, t1B);

                var m1A = posCornerPoint;
                var m1B = new PointFT(0, redLinePoint + 5);
                _meleeLines.Add(new Tuple<PointFT, PointFT>(m1A, m1B));

                var m2A = negCornerPoint;
                var m2B = new PointFT(0, redLinePoint + 5);
                _meleeLines.Add(new Tuple<PointFT, PointFT>(m2A, m2B));

                var r1A = new PointFT(posCornerPoint.X, posCornerPoint.Y + 2);
                var r1B = new PointFT(0, redLinePoint + 7);
                _rangedLines.Add(new Tuple<PointFT, PointFT>(r1A, r1B));

                var r2A = new PointFT(negCornerPoint.X, negCornerPoint.Y + 2);
                var r2B = new PointFT(0, redLinePoint + 7);
                _rangedLines.Add(new Tuple<PointFT, PointFT>(r2A, r2B));
            }
            else // (orgin.Y < 0)
            {
                var redLinePoint = GameGrid.RedPoints.Where(point => point.X > -10 && point.X < 10)
                    .Min(point => point.Y);

                var adjustedRedPoints =
                    GameGrid.RedPoints.Where(point => point.X > GameGrid.MinX + 5 && point.X < GameGrid.MaxX - 5)
                        .ToList();

                // get the corner deployment point based on the largest xy in the quadrant.
                var posCornerPoint = adjustedRedPoints.OrderByDescending(point => point.X - point.Y).First();

                var negCornerPoint = adjustedRedPoints.OrderByDescending(point => -point.X - point.Y).First();

                _healPoint = new PointFT(0, redLinePoint + 9);
                _ragePoint = new PointFT(0, redLinePoint + 12);

                var t1A = new PointFT(GameGrid.MaxX - 10, redLinePoint);
                var t1B = new PointFT(GameGrid.MinX + 10, redLinePoint);
                _tankLine = new Tuple<PointFT, PointFT>(t1A, t1B);

                var m1A = posCornerPoint;
                var m1B = new PointFT(0, redLinePoint - 5);
                _meleeLines.Add(new Tuple<PointFT, PointFT>(m1A, m1B));

                var m2A = negCornerPoint;
                var m2B = new PointFT(0, redLinePoint - 5);
                _meleeLines.Add(new Tuple<PointFT, PointFT>(m2A, m2B));

                var r1A = new PointFT(posCornerPoint.X, posCornerPoint.Y - 2);
                var r1B = new PointFT(0, redLinePoint + 7);
                _rangedLines.Add(new Tuple<PointFT, PointFT>(r1A, r1B));

                var r2A = new PointFT(negCornerPoint.X, negCornerPoint.Y - 2);
                var r2B = new PointFT(0, redLinePoint + 7);
                _rangedLines.Add(new Tuple<PointFT, PointFT>(r2A, r2B));
            }
        }

        private void VisualizeDeployment()
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    var p1 = new PointFT(x: 0, y: 0).ToScreenAbsolute();
                    var p2 = new PointFT(x: 0, y: 5).ToScreenAbsolute();
                    var distance = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

                    foreach (var meleeLine in _meleeLines)
                        g.DrawLine(new Pen(Color.Red), meleeLine.Item1.ToScreenAbsolute(), meleeLine.Item2.ToScreenAbsolute());

                    foreach (var rangedLine in _rangedLines)
                        g.DrawLine(new Pen(Color.Red), rangedLine.Item1.ToScreenAbsolute(), rangedLine.Item2.ToScreenAbsolute());

                    foreach (PointFT redPoint in GameGrid.RedPoints)
                        g.FillEllipse(Brushes.Red, redPoint.ToScreenAbsolute().ToRectangle(3, 3));

                    g.DrawLine(new Pen(Color.Red), _tankLine.Item1.ToScreenAbsolute(), _tankLine.Item2.ToScreenAbsolute());

                    g.FillEllipse(new SolidBrush(Color.FromArgb(128, Color.Gold)),
                        _healPoint.ToScreenAbsolute().ToRectangle((int)distance, (int)distance));

                    g.FillEllipse(new SolidBrush(Color.FromArgb(128, Color.Magenta)),
                        _ragePoint.ToScreenAbsolute().ToRectangle((int)distance, (int)distance));
                }
                var d = DateTime.UtcNow;
                Screenshot.Save(bmp, $"Breakthrough Deploy {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}");
            }
        }
    }
}
