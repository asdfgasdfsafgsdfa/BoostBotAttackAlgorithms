using CoC_Bot.API;
using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot;
using CoC_Bot.API.Buildings;
using System.Drawing;
using System.Threading;

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
            // CreateDeployPoints();
            // VisualizeDeployment();

            return Opponent.MeetsRequirements(BaseRequirements.All) ? 1 : 0;
        }

        private PointFT _orgin, _healPoint, _ragePoint, _healerPoint, _qwPoint, _queenRagePoint;

        private PointFT[] _tankPoints;

        private Tuple<PointFT, PointFT> _attackLine;

        private DeployElement _freezeSpell;

        private void DropFreeze(object sender, EventArgs a)
        {
            var inferno = (InfernoTower)sender;

            foreach (var t in Deploy.AtPoint(_freezeSpell, inferno.Location.GetCenter()))
                Thread.Sleep(t);

            inferno.StopWatching();
        }

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info("[Breakthrough] Deploy start");

            var waveTroopNames = new[] { "Archer", "Barbarian", "Minion" };
            var allByLineNames = new[] { "Wizard", "Balloon", "Dragon", "Baby Dragon", "Miner" };

            // TODO: Update once pekka is fixed in data.bin
            var allByPointNames = new[] { "Valkyrie", "pekka", "Witch", "Goblin" };

            // get a list of all deployable units
            var deployElements = Deploy.GetTroops();

            // extract spells into their own list
            var spells = deployElements.Extract(u => u.ElementType == DeployElementType.Spell).ToList();

            // extract heores into their own list
            var heroes = deployElements.Extract(u => u.IsHero).ToList();

            // extract clanCastle into its own list
            var clanCastle = deployElements.ExtractOne(u => u.ElementType == DeployElementType.ClanTroops);

            // get tanks
            var tanks = deployElements.Extract(u => u.UnitData?.AttackType == AttackType.Tank).ToArray();

            // get wallbreakers
            var wallBreakers = deployElements.ExtractOne(u => u.UnitData?.AttackType == AttackType.Wallbreak);

            // get healers
            var healers = deployElements.ExtractOne(u => u.UnitData?.AttackType == AttackType.Heal);

            // get deploy in waves
            var waveTroops = deployElements.Extract(u => waveTroopNames.Contains(u.PrettyName)).ToArray();

            // get deploy all in a line
            var allByLine = deployElements.Extract(u => allByLineNames.Contains(u.PrettyName)).ToArray();

            // get deploy all by point
            var allByPoint = deployElements.Extract(u => allByPointNames.Contains(u.PrettyName)).ToArray();

            var bowlers = deployElements.ExtractOne(u => u.PrettyName == "Bowler");

            // get hogs
            var hogs = deployElements.ExtractOne(u => u.PrettyName == "Hog");

            // get heal spells
            var healSpells = spells.ExtractOne(u => u.PrettyName == "Heal");

            // get rage spells
            var rageSpells = spells.ExtractOne(u => u.PrettyName == "Rage");

            // user's wave delay setting
            var waveDelay = (int)(UserSettings.WaveDelay * 1000);

            // check if queen walk is an option
            if (healers?.Count >= 4 && heroes.Any(u => u.ElementType == DeployElementType.HeroQueen))
            {
                // get deploy points with queen walk
                CreateDeployPoints(true);

                var queen = heroes.ExtractOne(u => u.ElementType == DeployElementType.HeroQueen);

                // deploy queen walk
                Log.Info("[Breakthrough] Queen walk available.");
                Log.Info($"[Breakthrough] Deploying {queen.PrettyName}");
                foreach (var t in Deploy.AtPoint(queen, _qwPoint, waveDelay: waveDelay))
                    yield return t;

                var healerCount = Math.Min(healers.Count, 4);
                Log.Info($"[Breakthrough] Deploying {healers.PrettyName} x{healerCount}");
                foreach (var t in Deploy.AtPoint(healers, _healerPoint, healerCount, waveDelay: waveDelay))
                    yield return t;

                // watch queen
                Deploy.WatchHeroes(new List<DeployElement> { queen });

                if (rageSpells?.Count > 1)
                {
                    Log.Info($"[Breakthrough] Deploying {rageSpells.PrettyName} x1");
                    foreach (var t in Deploy.AtPoint(rageSpells, _queenRagePoint, waveDelay: waveDelay))
                        yield return t;
                }

                // wait 15 seconds
                yield return 15000;
            }
            else
            {
                // get deploy points without queen walk
                CreateDeployPoints(false);
            }


            // deploy two tanks
            foreach (var unit in tanks.Where(u => u.Count > 1))
            {
                Log.Info($"[Breakthrough] Deploying {unit.PrettyName} x2");
                foreach (var t in Deploy.AtPoints(unit, _tankPoints, waveDelay: waveDelay))
                    yield return t;
            }

            // deploy Wallbreakers
            while (wallBreakers?.Count > 0)
            {
                var count = wallBreakers.Count;

                Log.Info($"[Breakthrough] Deploying {wallBreakers.PrettyName} x3");
                foreach (var t in Deploy.AtPoint(wallBreakers, _orgin, 3))
                    yield return t;

                // prevent infinite loop if deploy point is on red
                if (wallBreakers.Count != count) continue;

                Log.Warning($"[Breakthrough] Couldn't deploy {wallBreakers.PrettyName}");
                break;
            }

            // deploy funnel waves
            while (waveTroops.Any(u => u.Count > 0))
            {
                foreach (var unit in waveTroops.Where(u => u.Count > 0))
                {
                    // get the remaining troops divied by the remaining lines
                    var deployElementCount = Math.Min(unit.Count, UserSettings.WaveSize);

                    Log.Info($"[Breakthrough] Deploying {unit.PrettyName} x{deployElementCount}");
                    foreach (
                        var t in
                            Deploy.AlongLine(unit, _attackLine.Item1, _attackLine.Item2, deployElementCount, 4,
                                waveDelay: waveDelay))
                        yield return t;
                }
            }

            // deploy the rest of the tanks
            while (tanks.Any(u => u.Count > 0))
            {
                var deployError = false;

                foreach (var unit in tanks.Where(u => u.Count > 0))
                {
                    var count = unit.Count;

                    Log.Info($"[Breakthrough] Deploying {unit.PrettyName} x{unit.Count}");
                    foreach (var t in Deploy.AtPoint(unit, _orgin, unit.Count, waveDelay: waveDelay))
                        yield return t;

                    // prevent infinite loop if deploy point is on red
                    if (unit.Count != count) continue;

                    Log.Warning($"[Breakthrough] Couldn't deploy {unit.PrettyName}");
                    deployError = true;
                    break;
                }
                if (deployError) break;
            }

            while (allByLine.Any(u => u.Count > 0))
            {
                foreach (var unit in allByLine.Where(u => u.Count > 0))
                {
                    Log.Info($"[Breakthrough] Deploying {unit.PrettyName} x{unit.Count}");
                    foreach (
                        var delay in
                            Deploy.AlongLine(unit, _attackLine.Item1, _attackLine.Item2, unit.Count, 4,
                                waveDelay: waveDelay))
                        yield return delay;
                }
            }

            if (rageSpells?.Count > 0)
            {
                Log.Info($"[Breakthrough] Deploying {rageSpells.PrettyName} x1");
                foreach (var t in Deploy.AtPoint(rageSpells, _ragePoint, waveDelay: waveDelay))
                    yield return t;
            }

            if (healSpells?.Count > 0)
            {
                Log.Info($"[Breakthrough] Deploying {healSpells.PrettyName} x1");
                foreach (var t in Deploy.AtPoint(healSpells, _healPoint, waveDelay: waveDelay))
                    yield return t;
            }

            while (allByPoint.Any(u => u.Count > 0))
            {
                var deployError = false;

                foreach (var unit in allByPoint.Where(u => u.Count > 0))
                {
                    var count = unit.Count;

                    Log.Info($"[Breakthrough] Deploying {unit.PrettyName} x{unit.Count}");
                    foreach (var t in Deploy.AtPoint(unit, _orgin, unit.Count, waveDelay: waveDelay))
                        yield return t;

                    // prevent infinite loop if deploy point is on red
                    if (unit.Count != count) continue;

                    Log.Warning($"[Breakthrough] Couldn't deploy {unit.PrettyName}");
                    deployError = true;
                    break;
                }
                if (deployError) break;
            }

            if (bowlers?.Count > 0)
            {
                Log.Info($"[Breakthrough] Deploying {bowlers.PrettyName} x{bowlers.Count}");
                foreach (var t in Deploy.AtPoint(bowlers, _orgin, bowlers.Count, waveDelay: waveDelay))
                    yield return t;
            }

            if (clanCastle?.Count > 0)
            {
                Log.Info($"[Breakthrough] Deploying {clanCastle.PrettyName}");
                foreach (var t in Deploy.AtPoint(clanCastle, _orgin, waveDelay: waveDelay))
                    yield return t;
            }

            if (heroes.Any())
            {
                foreach (var hero in heroes.Where(u => u.Count > 0))
                {
                    Log.Info($"[Breakthrough] Deploying {hero.PrettyName}");
                    foreach (var t in Deploy.AtPoint(hero, _orgin, waveDelay: waveDelay))
                        yield return t;
                }
            }

            if (healers?.Count > 0)
            {
                Log.Info($"[Breakthrough] Deploying {healers.PrettyName} x{healers.Count}");
                foreach (var t in Deploy.AtPoint(healers, _healerPoint, healers.Count, waveDelay: waveDelay))
                    yield return t;
            }

            if (hogs?.Count > 0)
            {
                Log.Info($"[Breakthrough] Deploying {hogs.PrettyName} x{hogs.Count}");
                foreach (var t in Deploy.AtPoints(hogs, _tankPoints, hogs.Count/2, waveDelay: waveDelay))
                    yield return t;
            }

            Deploy.WatchHeroes(heroes);

            // get freeze spells
            _freezeSpell = spells.ExtractOne(u => u.PrettyName == "Freeze");

            // no freeze spells so end deployment
            if (!(_freezeSpell?.Count > 0)) yield break;

            // find and watch inferno towers
            var infernos = InfernoTower.Find();

            foreach (var inferno in infernos)
            {
                inferno.FirstActivated += DropFreeze;

                inferno.StartWatching();
            }
        }

        private void CreateDeployPoints(bool qw)
        {
            var target = DarkElixirStorage.Find().FirstOrDefault()?.Location.GetCenter() ??
                         TownHall.Find()?.Location.GetCenter() ?? new PointFT(0, 0);

            var maxRedPointX = GameGrid.RedPoints.Max(point => point.X) + 1;
            var minRedPointX = GameGrid.RedPoints.Min(point => point.X) + 1;
            var maxRedPointY = GameGrid.RedPoints.Max(point => point.Y) + 1;
            var minRedPointY = GameGrid.RedPoints.Min(point => point.Y) + 1;

            var left = new PointFT(minRedPointX, maxRedPointY);
            var top = new PointFT(maxRedPointX, maxRedPointY);
            var right = new PointFT(maxRedPointX, minRedPointY);
            var bottom = new PointFT(minRedPointX, minRedPointY);

            var orginPoints = new[]
            {
                new PointFT(maxRedPointX, 0),
                new PointFT(minRedPointX, 0),
                new PointFT(0, maxRedPointY),
                new PointFT(0, minRedPointY)
            };

            _orgin = orginPoints.OrderBy(point => point.DistanceSq(target)).First();

            const float tankOffset = 6f;

            if (_orgin.X > 0)
            {
                Log.Info("[Breakthrough] Attacking from the top right");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.Y > -10 && point.Y < 10)
                    .Max(point => point.X);

                if (qw)
                {
                    _qwPoint = right;
                    _queenRagePoint = new PointFT(right.X - 5, right.Y + 5);
                    _healerPoint = new PointFT(24f, -24f);
                }
                else
                {
                    _healerPoint = new PointFT(24f, 0f);
                }
                _healPoint = new PointFT(redLinePoint - 12f, 0f);
                _ragePoint = new PointFT(redLinePoint - 9f, 0f);
                _attackLine = new Tuple<PointFT, PointFT>(top, right);
                _tankPoints = new[]
                {new PointFT(_orgin.X, _orgin.Y + tankOffset), new PointFT(_orgin.X, _orgin.Y - tankOffset)};
            }
            else if (_orgin.X < 0)
            {
                Log.Info("[Breakthrough] Attacking from the bottom left");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.Y > -10 && point.Y < 10)
                    .Min(point => point.X);

                if (qw)
                {
                    _qwPoint = left;
                    _queenRagePoint = new PointFT(left.X + 5, left.Y - 5);
                    _healerPoint = new PointFT(-24f, 24f);
                }
                else
                {
                    _healerPoint = new PointFT(-24f, 0f);
                }
                _healPoint = new PointFT(redLinePoint + 12, 0f);
                _ragePoint = new PointFT(redLinePoint + 9, 0f);
                _attackLine = new Tuple<PointFT, PointFT>(bottom, left);
                _tankPoints = new[]
                {new PointFT(_orgin.X, _orgin.Y + tankOffset), new PointFT(_orgin.X, _orgin.Y - tankOffset)};
            }
            else if (_orgin.Y > 0)
            {
                Log.Info("[Breakthrough] Attacking from the top left");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.X > -10 && point.X < 10)
                    .Max(point => point.Y);

                if (qw)
                {
                    _qwPoint = left;
                    _queenRagePoint = new PointFT(left.X + 5f, left.Y - 5f);
                    _healerPoint = new PointFT(-24f, 24f);
                }
                else
                {
                    _healerPoint = new PointFT(0f, 24f);
                }
                _healPoint = new PointFT(0, redLinePoint - 12f);
                _ragePoint = new PointFT(0, redLinePoint - 9f);
                _attackLine = new Tuple<PointFT, PointFT>(left, top);
                _tankPoints = new[] { new PointFT(_orgin.X + tankOffset, _orgin.Y), new PointFT(_orgin.X - tankOffset, _orgin.Y) };
            }
            else // (orgin.Y < 0)
            {
                Log.Info("[Breakthrough] Attacking from the bottom right");

                var redLinePoint = GameGrid.RedPoints
                    .Where(point => point.X > -10 && point.X < 10)
                    .Min(point => point.Y);

                if (qw)
                {
                    _qwPoint = right;
                    _queenRagePoint = new PointFT(right.X - 5, right.Y + 5);
                    _healerPoint = new PointFT(24f, -24f);
                }
                else
                {
                    _healerPoint = new PointFT(0f, -24f);
                }
                _healPoint = new PointFT(0f, redLinePoint + 12);
                _ragePoint = new PointFT(0f, redLinePoint + 9);
                _attackLine = new Tuple<PointFT, PointFT>(right, bottom);
                _tankPoints = new[] { new PointFT(_orgin.X + tankOffset, _orgin.Y), new PointFT(_orgin.X - tankOffset, _orgin.Y) };
            }
        }

        private void VisualizeDeployment()
        {
            using (var bmp = Screenshot.Capture())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    var p1 = new PointFT(0f, 0f).ToScreenAbsolute();
                    var p2 = new PointFT(0f, 5f).ToScreenAbsolute();
                    var distance = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

                    foreach (PointFT tankPoint in _tankPoints)
                        g.FillEllipse(Brushes.Red, tankPoint.ToScreenAbsolute().ToRectangle(3, 3));

                    g.DrawLine(new Pen(Color.Red, 2), _attackLine.Item1.ToScreenAbsolute(), _attackLine.Item2.ToScreenAbsolute());

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
