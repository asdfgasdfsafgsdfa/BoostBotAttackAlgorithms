using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot;
using CoC_Bot.API;
using System.Drawing;

[assembly: Addon("SixteenFingersDeploy", "4-fingers deploy simultaneously at all 4 sides", "AngryDog")]
namespace SixteenFingersDeploy
{

    [AttackAlgorithm("Sixteen Fingers Deploy", "Deploys units, with 16 fingers")]
    public class SixteenFingersDeploy : BaseAttack
    {

        public SixteenFingersDeploy(Opponent opponent) : base(opponent)
        {

        }

        public override double ShouldAccept()
        {
            // check if the base meets the user's requirements
            if (!Opponent.MeetsRequirements(BaseRequirements.All))
            {
                Log.Debug($"[SixteenFingersDeploy] Skipping this base because it doesn't meet the requirements.");
                return 0;
            }
            return 0.7;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info("[16 Fingers] Attack start");

            // Get all the units available
            Log.Debug("Scanning troops");

            var allDeployElements = Attack.GetAvailableDeployElements();
            var unitDeployElements = allDeployElements.Where(x => x.UnitData != null).ToArray();
            var tankUnits =
                unitDeployElements.Where(
                    x => x.ElementType == DeployElementType.NormalUnit && x.UnitData.AttackType == AttackType.Tank)
                    .ToArray();
            var attackUnits =
                unitDeployElements.Where(
                    x => x.ElementType == DeployElementType.NormalUnit && x.UnitData.AttackType == AttackType.Damage)
                    .ToArray();
            var healUnits =
                unitDeployElements.Where(
                    x => x.ElementType == DeployElementType.NormalUnit && x.UnitData.AttackType == AttackType.Heal)
                    .ToArray();
            var wallbreakUnits =
                unitDeployElements.Where(
                    x => x.ElementType == DeployElementType.NormalUnit && x.UnitData.AttackType == AttackType.Wallbreak)
                    .ToArray();

            Dictionary<string, DeployElement[]> unitGroups = new Dictionary<string, DeployElement[]>()
            {
                {"tank units", tankUnits},
                {"attack units", attackUnits},
                {"heal units", healUnits},
                {"wallbreak units", wallbreakUnits},
            };

            var heroesAndClanCastle =
                allDeployElements.Extract(
                    deployElement => deployElement.ElementType == DeployElementType.ClanTroops || deployElement.IsHero);

            PointFT left = new PointFT(PointFT.MinRedZoneX, PointFT.MaxRedZoneY);
            PointFT top = new PointFT(PointFT.MaxRedZoneX, PointFT.MaxRedZoneY);
            PointFT right = new PointFT(PointFT.MaxRedZoneX, PointFT.MinRedZoneY);
            PointFT bottom = new PointFT(PointFT.MinRedZoneX, PointFT.MinRedZoneY);

            Tuple<PointFT, PointFT>[] lines =
            {
                new Tuple<PointFT, PointFT>(left, top),
                new Tuple<PointFT, PointFT>(right, top),
                new Tuple<PointFT, PointFT>(left, bottom),
                new Tuple<PointFT, PointFT>(right, bottom),
            };

            foreach (var unitGroup in unitGroups)
            {
                Logger.Info("[16 Fingers] Deploying " + unitGroup.Key);
                foreach (var y in Deploy.AlongLines(unitGroup.Value, lines, 4))
                    yield return y;
            }

            Logger.Info("[16 Fingers] Deploying heroes");
            var heroPoint = new PointFT((lines[0].Item1.X + lines[0].Item2.X) / 2, (lines[0].Item1.Y + lines[0].Item2.Y) / 2);

            foreach (var deployElement in heroesAndClanCastle.Where(deployElement => deployElement?.Count > 0))
            {
                Log.Info($"[16 Fingers] Deploying {deployElement.PrettyName}");
                foreach (var delay in Deploy.AtPoint(new[] { deployElement }, heroPoint))
                    yield return delay;
                yield return (int)(UserSettings.WaveDelay * 1000);
            }

            // Remove clan castle before watching heroes
            heroesAndClanCastle.ExtractOne(x => x.ElementType == DeployElementType.ClanTroops);
            Deploy.WatchHeroes(heroesAndClanCastle);

            Logger.Info("[16 Fingers] Deploy done");
        }

        public override string ToString()
        {
            return "Sixteen Fingers Deploy ©AngryDog";
        }
    }
}
