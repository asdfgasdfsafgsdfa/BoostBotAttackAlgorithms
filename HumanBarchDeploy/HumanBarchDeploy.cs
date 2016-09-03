﻿using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using CoC_Bot.Internals;
using System.Threading;
using SharedCode;

[assembly: Addon("HumanBarchDeploy Addon", "Contains the Human Barch deploy algorithm", "Bert")]

namespace HumanBarchDeploy
{
    [AttackAlgorithm("HumanBarchDeploy", "Deploys Barch units close to collectors in a believeable Human pattern.  (So that a review of the attack does not look like a BOT)")]
    class HumanBarchDeploy : BaseAttack
    {
        private float _minimumDistanceToCollectors = 9f;
        private float _minimumAttackDistanceToCollectors = 18f; //Use a slightly different number once we are already attacking... - Get collectors behind other buildings also...
        private int _minimumExposedTargets = 6;
        private float _thDeployRadius = 1.2f;
        private float _collectorDeployRadius = 1.4f;

        public HumanBarchDeploy(Opponent opponent) : base(opponent) { }

        public override string ToString()
        {
            return "Humanlike Barch Deploy";
        }

        public override double ShouldAccept()
        {
            int returnVal = 0;

            // set flags to only check elixir and gold against the user's settings
            var requirementsToCheck = BaseRequirements.Elixir | BaseRequirements.Gold;

            // check if the base meets the user's requirements
            if (!Opponent.MeetsRequirements(requirementsToCheck))
            {
                return returnVal;
            }

            //Check if the base is dead.
            if (Opponent.IsDead(true))
            {
                //Check how many Collectors are Ripe for the taking (outside walls)
                int ripeCollectors = HumanLikeAlgorithms.CountRipeCollectors(_minimumDistanceToCollectors);

                Log.Info($"[Human Barch] {ripeCollectors} collectors found outside walls.");

                if (ripeCollectors < _minimumExposedTargets)
                {
                    Log.Info($"[Human Barch] Skipping - {ripeCollectors} collectors were found outside the wall");
                    returnVal = 0;
                }
                else
                {
                    return 1;
                }
            }
            else
            {
                TownHall townHall = TownHall.Find(CacheBehavior.Default);

                if (townHall.CanSnipe())
                {
                    //The TH is positioned so we might be able to snipe it.
                    Log.Info($"[Human Barch] Sniping Active Town Hall!");
                    return 1;
                }
                else
                {
                    Log.Info($"[Human Barch] Skipping Active Base, TH is not Snipable.");
                    //This is a live base, and we can't snipe the TH.  Ignore the Loot Requirements, and always Skip.
                    returnVal = 0;
                }
            }

            return returnVal;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Debug("[Human Barch] Deploy start");

            var waveCounter = 1;

            //Check if we can snipe the town hall, and if so, what are the Deployment points for Gruns/Ranged.
            TownHall townHall = TownHall.Find(CacheBehavior.Default);

            Target townHallTarget = townHall.GetSnipeDeployPoints();

            // Get starting resources
            LootResources preLoot = Opponent.GetAvailableLoot();

            if (preLoot == null)
            {
                Log.Error("[Human Barch] Could not read available starting loot");
                Attack.Surrender();
                yield break;
            }
            Log.Info($"[Human Barch] Pre-attack resources - G: {preLoot.Gold}, E: {preLoot.Elixir}, DE: {preLoot.DarkElixir}");

            var collectorCacheBehavior = CacheBehavior.Default;
            var collectorCount = 0;
            var isDead = Opponent.IsDead(true);

            // Loop until surrender conditions are met
            while (true)
            {

                // Get all the units available
                Log.Info($"[Human Barch] Scanning troops for wave {waveCounter}");

                var allElements = Attack.GetAvailableDeployElements();
                var deployElements = allElements.Where(x => x.UnitData != null).ToArray();
                var rangedUnits = deployElements.Where(x => x.IsRanged == true && x.ElementType == DeployElementType.NormalUnit && x.UnitData.AttackType == AttackType.Damage);
                var gruntUnits = deployElements.Where(x => x.IsRanged == false && x.ElementType == DeployElementType.NormalUnit && x.UnitData.AttackType == AttackType.Damage);
                List<DeployElement> king = allElements.Where(x => x.Id == DeployId.King).ToList();
                List<DeployElement> queen = allElements.Where(x => x.Id == DeployId.Queen).ToList();

                //Dont Deploy any Tank Units... even if we have them.

                if (!isDead)
                {
                    if (townHallTarget.ValidTarget)
                    {
                        //Before we enter the main attack routine... If there is an exposed TH, Snipe it.
                        //If there are Teslas around it, oh well. we only spent 9-12 units  of each type trying.
                        if (gruntUnits.Any())
                        {
                            var gruntsToDeploy = Rand.Int(5, 15);
                            Log.Info($"[Human Barch] Sniping Town Hall {gruntsToDeploy} Grunts Near: X:{townHallTarget.DeployGrunts.X} Y:{townHallTarget.DeployGrunts.Y}");
                            foreach (var t in Deploy.AtPoints(gruntUnits.FilterTypesByCount(), townHallTarget.DeployGrunts.RandomPointsInArea(_thDeployRadius, gruntsToDeploy), 1, Rand.Int(10, 40), Rand.Int(10, 40)))
                                yield return t;
                            //Wait almost a second
                            yield return Rand.Int(300, 500); //Wait 
                        }

                        if (rangedUnits.Any())
                        {
                            var rangedToDeploy = Rand.Int(5, 15);
                            Log.Info($"[Human Barch] Sniping Town Hall {rangedToDeploy} Ranged Near: X:{townHallTarget.DeployRanged.X} Y:{townHallTarget.DeployRanged.Y}");
                            foreach (var t in Deploy.AtPoints(rangedUnits.FilterTypesByCount(), townHallTarget.DeployRanged.RandomPointsInArea(_thDeployRadius, rangedToDeploy), 1, Rand.Int(10, 40), Rand.Int(10, 40)))
                                yield return t;
                            //Wait almost a second
                            yield return Rand.Int(300, 500); //Wait 
                        }

                        //If we dont have a star yet, Drop the King...
                        if (!Attack.HaveAStar())
                        {
                            if (UserSettings.UseKing && king.Any())
                            {
                                Log.Info($"[Human Barch] Deploying King at: X:{townHallTarget.DeployGrunts.X} Y:{townHallTarget.DeployGrunts.Y}");
                                foreach (var t in DeployHeroes(king, townHallTarget.DeployGrunts.ToScreenAbsolute().ToEnumerable(), false))
                                    yield return t;
                                yield return Rand.Int(900, 1000); //Wait 
                            }

                            //Deploy the Queen
                            if (UserSettings.UseQueen && queen.Any())
                            {
                                Log.Info($"[Human Barch] Deploying Queen at: X:{townHallTarget.DeployRanged.X} Y:{townHallTarget.DeployRanged.Y}");
                                foreach (var t in DeployHeroes(queen, townHallTarget.DeployRanged.ToScreenAbsolute().ToEnumerable(), false))
                                    yield return t;
                                yield return Rand.Int(900, 1000); //Wait 
                            }
                        }

                        //Only try once to snipe the town hall when deploying waves.
                        townHallTarget.ValidTarget = false;
                    }

                }
                else
                {
                    //First time through use cached... after the first wave always recheck for Destroyed ones...
                    Target[] targets = HumanLikeAlgorithms.GenerateTargets(_minimumAttackDistanceToCollectors, collectorCacheBehavior);
                    collectorCount = targets.Length;

                    //Reorder the Deploy points so they look more human like when attacking.
                    var groupedTargets = targets.ReorderToClosestNeighbor().GroupCloseTargets();

                    collectorCacheBehavior = CacheBehavior.CheckForDestroyed;

                    if (collectorCount < 1)
                    {
                        Log.Info($"[Human Barch] Surrendering - Collectors Remaining = {collectorCount}");

                        // Wait for the wave to finish
                        Log.Info("[Human Barch] Deploy done. Waiting to finish...");
                        foreach (var t in Attack.WaitForNoResourceChange(10))
                            yield return t;

                        break;
                    }

                    if (townHallTarget.ValidTarget)
                    {
                        //Drop some Grunt and Ranged troups on the TH as well as collectors.
                        //If there are Teslas around it, oh well. we only spent 9-12 units  of each type trying.
                        if (gruntUnits.Any())
                        {
                            var gruntsToDeploy = Rand.Int(4, 6);
                            Log.Info($"[Human Barch] + TH Snipe Dead {gruntsToDeploy} Grunts Near: X:{townHallTarget.DeployGrunts.X} Y:{townHallTarget.DeployGrunts.Y}");
                            foreach (var t in Deploy.AtPoints(gruntUnits.FilterTypesByCount(), townHallTarget.DeployGrunts.RandomPointsInArea(_thDeployRadius, gruntsToDeploy), 1, Rand.Int(10, 40), Rand.Int(10, 40)))
                                yield return t;
                            yield return Rand.Int(300, 500); //Wait 
                        }

                        if (rangedUnits.Any())
                        {
                            var rangedToDeploy = Rand.Int(4, 6);
                            Log.Info($"[Human Barch] + TH Snipe Dead {rangedToDeploy} Ranged Near: X:{townHallTarget.DeployRanged.X} Y:{townHallTarget.DeployRanged.Y}");
                            foreach (var t in Deploy.AtPoints(rangedUnits.FilterTypesByCount(), townHallTarget.DeployRanged.RandomPointsInArea(_thDeployRadius, rangedToDeploy), 1, Rand.Int(10, 40), Rand.Int(10, 40)))
                                yield return t;
                            yield return Rand.Int(300, 500); //Wait 
                        }

                        //Only do this once.
                        townHallTarget.ValidTarget = false;
                    }

                    //Determine the index of the 1st and 2nd largest set of targets all in a row.
                    var largestSetIndex = -1;
                    int largestSetCount = 0;
                    var secondLargestSetIndex = -1;
                    int secondLargestSetCount = 0;

                    for (int i = 0; i < groupedTargets.Count; i++)
                    {
                        if (groupedTargets[i].Length > largestSetIndex)
                        {
                            secondLargestSetCount = largestSetCount;
                            secondLargestSetIndex = largestSetIndex;
                            largestSetCount = groupedTargets[i].Length;
                            largestSetIndex = i;
                        }
                        else if (groupedTargets[i].Length > secondLargestSetIndex)
                        {
                            secondLargestSetCount = groupedTargets[i].Length;
                            secondLargestSetIndex = i;
                        }
                    }

                    Log.Info($"[Human Barch] {groupedTargets.Count} Target Groups, Largest has {largestSetCount} targets, Second Largest {secondLargestSetCount} targets.");

                    //Deploy Barch Units - In Groups on Sets of collectors that are close together.
                    for (int p = 0; p < groupedTargets.Count; p++)
                    {
                        //Deploy Grunts on the Set of Targets.
                        for (int i = 0; i < groupedTargets[p].Length; i++)
                        {
                            var gruntDeployPoint = groupedTargets[p][i].DeployGrunts;

                            if (gruntUnits.Any())
                            {
                                int decreaseFactor = 0;
                                if (i > 0)
                                    decreaseFactor = (int)Math.Ceiling(i / 2d);

                                var gruntsAtCollector = (Rand.Int(6, 8) - decreaseFactor);
                                Log.Info($"[Human Barch] {gruntsAtCollector} Grunts Around Point: X:{gruntDeployPoint.X} Y:{gruntDeployPoint.Y}");
                                foreach (var t in Deploy.AtPoints(gruntUnits.FilterTypesByCount(), gruntDeployPoint.RandomPointsInArea(_collectorDeployRadius, gruntsAtCollector), 1, Rand.Int(10, 40)))
                                    yield return t;
                                yield return Rand.Int(10, 40); //Wait
                            }
                        }
                        
                        //Pause inbetween switching units.
                        yield return Rand.Int(90, 100); //Wait

                        if (secondLargestSetIndex == p && secondLargestSetCount >= 3) {
                            //We are currently deploying to the 2nd largest set of Targets - AND its a set of 3 or more.
                            //Drop the King on the 2nd Target in the set.

                            if (UserSettings.UseKing && king.Any())
                            {
                                Log.Info($"[Human Barch] Deploying King at: X:{groupedTargets[p][1].DeployGrunts.X} Y:{groupedTargets[p][1].DeployGrunts.Y}");
                                foreach (var t in DeployHeroes(king, groupedTargets[p][1].DeployGrunts.ToScreenAbsolute().ToEnumerable(), true))
                                    yield return t;
                                yield return Rand.Int(900, 1000); //Wait 
                            }
                        }

                        if (largestSetIndex == p && largestSetCount >= 3)
                        {
                            //We are currently deploying to the largest set of Targets - AND its a set of 3 or more.
                            //Drop the Queen on the 2nd Target in the set.

                            if (UserSettings.UseQueen && queen.Any())
                            {
                                yield return Rand.Int(90, 100); //Wait before dropping Queen

                                Log.Info($"[Human Barch] Deploying Queen at: X:{groupedTargets[p][1].DeployRanged.X} Y:{groupedTargets[p][1].DeployRanged.Y}");
                                foreach (var t in DeployHeroes(queen, groupedTargets[p][1].DeployRanged.ToScreenAbsolute().ToEnumerable(), true))
                                    yield return t;
                                yield return Rand.Int(900, 1000); //Wait 
                            }
                        }

                        //Deploy Ranged units on same set of Targets.
                        for (int i = 0; i < groupedTargets[p].Length; i++)
                        {
                            var rangedDeployPoint = groupedTargets[p][i].DeployRanged;

                            if (rangedUnits.Any())
                            {
                                int decreaseFactor = 0;
                                if (i > 0)
                                    decreaseFactor = (int)Math.Ceiling(i / 2d);

                                var rangedAtCollector = (Rand.Int(5, 7) - decreaseFactor);
                                Log.Info($"[Human Barch] {rangedAtCollector} Ranged Around Point: X:{rangedDeployPoint.X} Y:{rangedDeployPoint.Y}");
                                foreach (var t in Deploy.AtPoints(rangedUnits.FilterTypesByCount(), rangedDeployPoint.RandomPointsInArea(_collectorDeployRadius, rangedAtCollector), 1, Rand.Int(10, 40)))
                                    yield return t;
                                yield return Rand.Int(40, 50); //Wait
                            }
                        }

                        yield return Rand.Int(90, 100); //Wait before switching units back to Grutns and deploying on next set of targets.
                    }
                }

                //Never deploy any Healing type Units.


                //wait a random number of seconds before the next round on all Targets...
                yield return Rand.Int(2000, 5000);

                // Get starting resources, cache needs to be false to force a new check
                LootResources postLoot = Opponent.GetAvailableLoot(false);
                if (postLoot == null)
                {
                    Log.Warning($"[Human Barch] Human Barch Deploy could not read available loot this wave");
                    postLoot = new LootResources() { Gold = -1, Elixir = -1, DarkElixir = -1 };
                }

                Log.Info($"[Human Barch] Wave {waveCounter} resources - G: {postLoot.Gold}, E: {postLoot.Elixir}, DE: {postLoot.DarkElixir}");
                int newGold = preLoot.Gold - postLoot.Gold;
                int newElixir = preLoot.Elixir - postLoot.Elixir;
                int newDark = preLoot.DarkElixir - postLoot.DarkElixir;
                Log.Info($"[Human Barch] Wave {waveCounter} resource diff - G: {newGold}, E: {newElixir}, DE: {newDark}, Collectors: {collectorCount}");

                if (isDead)
                {
                    if (postLoot.Gold + postLoot.Elixir + postLoot.DarkElixir >= 0)
                    {
                        if (newGold + newElixir < 3000 * collectorCount)
                        {
                            Log.Info("[Human Barch] Surrendering because gained resources isn't enough");
                            break;
                        }
                        preLoot = postLoot;
                    }
                }
                else
                {
                    if (Attack.HaveAStar())
                    {
                        Log.Info("[Human Barch] We have a star! TH Sniped!");

                        //Check the Delta in Resources.
                        if (newGold + newElixir < (preLoot.Gold + preLoot.Elixir) * .05f) //Less than 5% of what is available.
                        {
                            //Switch the attack mode to Dead - so we get some of the collectors.
                            Log.Info($"[Human Barch] Not much loot gained from Snipe(G:{newGold} E:{newElixir} out of G:{preLoot.Gold} E:{preLoot.Elixir}) - Try to Loot Collectors also...");
                            isDead = true;
                        }
                        else
                        {
                            //Halt the Attack.
                            break;
                        }
                    }

                    if (waveCounter > 10)
                    {
                        Log.Info("[Human Barch] Fail! TH Not Sniped! our troops died - Surrendering...");
                        break;
                    }
                }

                waveCounter++;
            }

            //We broke out of the attack loop... 
            Attack.Surrender();
        }

    }
}

