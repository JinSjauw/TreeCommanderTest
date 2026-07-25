using BehaviourTree.Core;
using BehaviourTree.Runtime;
using NUnit.Framework;

namespace BehaviourTree.Tests
{
    public class ParamSchemaReflectionTests
    {
        private sealed class FixtureMethod : ActionMethod
        {
            [SharedVar(IsHidden = true, AutoVariableName = "AgentOrders")]
            public int ordersSlot;

            [SharedVar(isToggleVariable: true, IsOrderDropdown = true)]
            public int orderValue;

            public float plainConstant;
            public bool useCustomTick;
            public float customTickValue;

            public override NodeState Execute(TickContext ctx) => NodeState.SUCCESS;
        }

        private sealed class ParameterlessMethod : ActionMethod
        {
            public override NodeState Execute(TickContext ctx) => NodeState.SUCCESS;
        }

        [Test]
        public void Projection_MapsAttributes()
        {
            var d = ParamSchemaReflection.GetDescriptors(typeof(FixtureMethod));
            Assert.AreEqual(5, d.Length);

            Assert.AreEqual(DynamicParamKind.Variable, d[0].kind);
            Assert.IsTrue(d[0].isHidden);
            Assert.AreEqual("AgentOrders", d[0].autoVariableName);
            Assert.AreEqual("ordersSlot", d[0].fieldName);

            Assert.AreEqual(DynamicParamKind.Toggle, d[1].kind);
            Assert.AreEqual(ConstantEditorHint.OrderDropdown, d[1].constantEditor);
            Assert.AreEqual(typeof(int), d[1].allowedTypes[0]);

            Assert.AreEqual(DynamicParamKind.Constant, d[2].kind);
            Assert.AreEqual("plainConstant", d[2].fieldName);
        }

        [Test]
        public void Projection_CustomTickValueDependsOnPreviousBool()
        {
            var d = ParamSchemaReflection.GetDescriptors(typeof(FixtureMethod));
            Assert.AreEqual("customTickValue", d[4].fieldName);
            Assert.AreEqual(3, d[4].visibilityDependsOnIndex);
        }

        [Test]
        public void Projection_NoPublicFields_ReturnsNull()
        {
            Assert.IsNull(ParamSchemaReflection.GetDescriptors(typeof(ParameterlessMethod)));
        }
    }
}
