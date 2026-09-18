using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Yijing.Domain.Configuration;
using Yijing.Editor;
using Yijing.Infrastructure;

namespace Yijing.Tests
{
    public sealed class ConfigurationTests
    {
        private PrototypeConfig config;

        [SetUp]
        public void LoadSource()
        {
            config = JsonUtility.FromJson<PrototypeConfig>(File.ReadAllText(ProjectSetup.SourcePath));
        }

        [Test] public void SourcePassesValidation() => ConfigValidator.Validate(config);
        [Test] public void ImportedAssetMatchesSource() => ProjectSetup.ValidateConfiguration();

        [Test]
        public void RuntimeSnapshotCannotMutateImportedAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PrototypeConfigAsset>(ProjectSetup.ConfigAssetPath);
            var original = asset.CreateSnapshot().items[0].baseUnits;
            asset.CreateSnapshot().items[0].baseUnits = 999;
            Assert.That(asset.CreateSnapshot().items[0].baseUnits, Is.EqualTo(original));
        }

        [Test] public void DuplicateItemIdIsRejected()
        {
            config.items[1].id = config.items[0].id;
            Assert.Throws<InvalidDataException>(() => ConfigValidator.Validate(config));
        }

        [Test] public void CrossChainUpgradeIsRejected()
        {
            config.items[0].nextId = "ceramic_02";
            Assert.Throws<InvalidDataException>(() => ConfigValidator.Validate(config));
        }

        [Test] public void UnequalChoiceCostsAreRejected()
        {
            config.orders[0].variants[0].requirements[0].quantity++;
            Assert.Throws<InvalidDataException>(() => ConfigValidator.Validate(config));
        }

        [Test] public void IncorrectRewardIsRejected()
        {
            config.orders[0].rewardStones++;
            Assert.Throws<InvalidDataException>(() => ConfigValidator.Validate(config));
        }

        [Test] public void UnknownRecipeItemIsRejected()
        {
            config.orders[0].variants[0].requirements[0].itemId = "unknown";
            Assert.Throws<InvalidDataException>(() => ConfigValidator.Validate(config));
        }

        [Test] public void MissingStoryOrderIsRejected()
        {
            config.storyNodes[0].orderIds[0] = "unknown";
            Assert.Throws<InvalidDataException>(() => ConfigValidator.Validate(config));
        }

        [Test] public void CircularStoryUnlockIsRejected()
        {
            config.storyNodes[0].prerequisiteStoryNodes = new[] { config.storyNodes.Last().id };
            Assert.Throws<InvalidDataException>(() => ConfigValidator.Validate(config));
        }

        [Test] public void RenovationCannotDependOnTheNodeItUnlocks()
        {
            config.storyNodes[0].prerequisiteRenovation = "lamp";
            Assert.Throws<InvalidDataException>(() => ConfigValidator.Validate(config));
        }

        [Test] public void BottomUpLinesAreNotBinaryKingWenNumbers()
        {
            var qian = config.oracleCards.Single(x => x.lowerBits == "001");
            Assert.That(qian.kingWenId, Is.EqualTo(15));
            qian.kingWenId = 1;
            Assert.Throws<InvalidDataException>(() => ConfigValidator.Validate(config));
        }

        [Test] public void MissingCardCombinationIsRejected()
        {
            config.oracleCards = config.oracleCards.Take(7).ToArray();
            Assert.Throws<InvalidDataException>(() => ConfigValidator.Validate(config));
        }

        [Test] public void MissingRulesFailClearly()
        {
            config.rules = null;
            Assert.Throws<InvalidDataException>(() => ConfigValidator.Validate(config));
        }

        [Test] public void PrologueMandatoryRenovationsAreAffordableWhenRequired()
        {
            var wallet = config.rules.initialStones;
            foreach (var order in config.orders)
            {
                wallet += order.rewardStones;
                foreach (var repair in config.renovations.Where(x => x.mandatory && x.requiredAfterOrder == order.id))
                {
                    wallet -= repair.cost;
                    Assert.That(wallet, Is.GreaterThanOrEqualTo(0), repair.id);
                }
            }
            Assert.That(wallet, Is.EqualTo(266));
        }
    }
}
