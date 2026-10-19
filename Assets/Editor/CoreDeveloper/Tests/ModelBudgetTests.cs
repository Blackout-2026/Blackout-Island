using NUnit.Framework;

namespace Blackout.Editor.Tests
{
    public class ModelBudgetTests
    {
        [Test]
        public void Generator_UsesTheDocumentedNumbers()
        {
            Assert.That(ModelBudget.TryGet("SM_Generator", out ModelBudget budget), Is.True);
            Assert.That(budget.TargetTriangles, Is.EqualTo(10000));
            Assert.That(budget.MaxTriangles, Is.EqualTo(15000));
            Assert.That(budget.MaxMaterials, Is.EqualTo(6));
        }

        [Test]
        public void Robot_UsesTheDocumentedNumbers()
        {
            Assert.That(ModelBudget.TryGet("SM_SecurityRobot", out ModelBudget budget), Is.True);
            Assert.That(budget.TargetTriangles, Is.EqualTo(5000));
            Assert.That(budget.MaxTriangles, Is.EqualTo(7500));
            Assert.That(budget.MaxMaterials, Is.EqualTo(4));
        }

        [Test]
        public void Prop_UsesTheLargePropLimit()
        {
            Assert.That(ModelBudget.TryGet("SM_Prop_LabBench", out ModelBudget budget), Is.True);
            Assert.That(budget.MaxTriangles, Is.EqualTo(4500));
        }

        [TestCase("SM_Char_Patrol", 4500)]
        [TestCase("SM_Weapon_Smg", 9000)]
        [TestCase("SM_Weapon_Knife", 9000)]
        [TestCase("SM_Weapon_Sniper", 9000)]
        public void Categories_GetTheirOwnMaximum(string assetName, int expectedMax)
        {
            Assert.That(ModelBudget.TryGet(assetName, out ModelBudget budget), Is.True);
            Assert.That(budget.MaxTriangles, Is.EqualTo(expectedMax));
        }

        [Test]
        public void VariantOfAHeroModel_KeepsTheHeroBudget()
        {
            Assert.That(ModelBudget.TryGet("SM_Generator_Lod1", out ModelBudget budget), Is.True);
            Assert.That(budget.TargetTriangles, Is.EqualTo(10000));
        }

        [TestCase("SM_GeneratorCable")]
        [TestCase("Tree")]
        [TestCase("")]
        [TestCase(null)]
        public void UnknownNames_HaveNoBudget(string assetName)
        {
            Assert.That(ModelBudget.TryGet(assetName, out _), Is.False);
        }

        [Test]
        public void HardMax_IsOneAndAHalfTimesTheTarget()
        {
            var budget = new ModelBudget(800, 2);
            Assert.That(budget.MaxTriangles, Is.EqualTo(1200));
        }
    }
}
