using System;
using BehaviourTree.Core;
using NUnit.Framework;

namespace BehaviourTree.Tests
{
    public class ParamsTests
    {
        private enum TestOp { A, B }

        [Test]
        public void Variable_SetsKindLabelsAndTypes()
        {
            var d = Params.Variable("Target", typeof(int), typeof(float));
            Assert.AreEqual(DynamicParamKind.Variable, d.kind);
            Assert.AreEqual("Target", d.titleLabel);
            Assert.AreEqual("Target", d.label);
            Assert.AreEqual(2, d.allowedTypes.Length);
        }

        [Test]
        public void Build_AssignsSequentialIndices()
        {
            var ds = Params.Build(
                Params.Variable("Source"),
                Params.Operation<TestOp>("Operation"),
                Params.Variable("Output").SyncElementTypeFrom(0));
            Assert.AreEqual(0, ds[0].index);
            Assert.AreEqual(1, ds[1].index);
            Assert.AreEqual(2, ds[2].index);
            Assert.AreEqual(0, ds[2].syncTypeFromIndex);
            Assert.IsTrue(ds[2].syncElementType);
        }

        [Test]
        public void Operation_SetsEnumTypeAndFilter()
        {
            Func<Type, int[]> filter = _ => new[] { 0 };
            var d = Params.Operation<TestOp>("Op", filter);
            Assert.AreEqual(DynamicParamKind.Operation, d.kind);
            Assert.AreEqual(typeof(TestOp), d.operationEnumType);
            Assert.AreSame(filter, d.getAvailableOpIndices);
        }

        [Test]
        public void Hidden_SetsAutoBind()
        {
            var d = Params.Hidden("AgentOrders");
            Assert.AreEqual(DynamicParamKind.Variable, d.kind);
            Assert.IsTrue(d.isHidden);
            Assert.AreEqual("AgentOrders", d.autoVariableName);
        }

        [Test]
        public void NoTitle_ClearsTitleLabel_KeepsLabel()
        {
            var d = Params.Variable("Target").NoTitle();
            Assert.IsNull(d.titleLabel);
            Assert.AreEqual("Target", d.label);
        }
    }
}
