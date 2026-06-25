using BehaviourTree.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BehaviourTree.Runtime.Tests
{
    /// <summary>
    /// Tests for TreeBaker.ValidateVariableType — ensures type-checking works
    /// consistently for stride=1 and stride>1 variables (Issue 1 fix).
    /// </summary>
    public class BakingTypeCheckTests
    {
        private BlackboardDefinition bbDef;
        private Dictionary<string, int> varIndexByName;

        [SetUp]
        public void SetUp()
        {
            bbDef = ScriptableObject.CreateInstance<BlackboardDefinition>();
            bbDef.name = "TestBB";
            varIndexByName = new Dictionary<string, int>();
        }

        [TearDown]
        public void TearDown()
        {
            if (bbDef != null)
                Object.DestroyImmediate(bbDef);
        }

        /// <summary>
        /// Adds a variable to the BB definition and the name→index map.
        /// </summary>
        private void AddVariable<T>(string name, int stride)
        {
            var variable = bbDef.AddVariable<T>(name, stride);
            int varIndex = bbDef.GetAllVariables().Count - 1;
            varIndexByName[name] = varIndex;
        }

        /// <summary>
        /// Creates a NodeFieldEntry for a variable field with the given name and type.
        /// </summary>
        private static NodeFieldEntry MakeEntry(string fieldName, string variableName, Type fieldType)
        {
            return new NodeFieldEntry
            {
                fieldName = fieldName,
                isVariable = true,
                variableName = variableName,
                fieldTypeName = fieldType.AssemblyQualifiedName,
            };
        }

        /// <summary>
        /// Shortcut: calls ValidateVariableType and returns whether the result is >= 0.
        /// </summary>
        private bool IsCompatible(string variableName, Type fieldType)
        {
            int index = varIndexByName.ContainsKey(variableName) ? varIndexByName[variableName] : -1;
            var entry = MakeEntry("testField", variableName, fieldType);
            int result = TreeBaker.ValidateVariableType(index, entry, bbDef);
            return result >= 0;
        }

        // ═══════════════════════════════════════════════════════════════
        // Valid: int field → int variable
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void IntField_IntVariable_Stride1_Compatible()
        {
            AddVariable<int>("IntVar", stride: 1);
            Assert.That(IsCompatible("IntVar", typeof(int)), Is.True,
                "int field should be compatible with int stride=1 variable");
        }

        [Test]
        public void IntField_IntVariable_Stride3_Compatible()
        {
            AddVariable<int>("IntArr3", stride: 3);
            Assert.That(IsCompatible("IntArr3", typeof(int)), Is.True,
                "int field should be compatible with int stride=3 variable");
        }

        [Test]
        public void IntField_IntVariable_Stride5_Compatible()
        {
            AddVariable<int>("IntArr5", stride: 5);
            Assert.That(IsCompatible("IntArr5", typeof(int)), Is.True,
                "int field should be compatible with int stride=5 variable");
        }

        // ═══════════════════════════════════════════════════════════════
        // Valid: float field → float variable
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void FloatField_FloatVariable_Stride1_Compatible()
        {
            AddVariable<float>("FloatVar", stride: 1);
            Assert.That(IsCompatible("FloatVar", typeof(float)), Is.True,
                "float field should be compatible with float stride=1 variable");
        }

        [Test]
        public void FloatField_FloatVariable_Stride3_Compatible()
        {
            AddVariable<float>("FloatArr3", stride: 3);
            Assert.That(IsCompatible("FloatArr3", typeof(float)), Is.True,
                "float field should be compatible with float stride=3 variable");
        }

        [Test]
        public void FloatField_FloatVariable_Stride5_Compatible()
        {
            AddVariable<float>("FloatArr5", stride: 5);
            Assert.That(IsCompatible("FloatArr5", typeof(float)), Is.True,
                "float field should be compatible with float stride=5 variable");
        }

        // ═══════════════════════════════════════════════════════════════
        // Valid: Vector3 field → Vector3 variable
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Vector3Field_Vector3Variable_Stride1_Compatible()
        {
            AddVariable<Vector3>("VecVar", stride: 1);
            Assert.That(IsCompatible("VecVar", typeof(Vector3)), Is.True,
                "Vector3 field should be compatible with Vector3 stride=1 variable");
        }

        [Test]
        public void Vector3Field_Vector3Variable_Stride3_Compatible()
        {
            AddVariable<Vector3>("VecArr3", stride: 3);
            Assert.That(IsCompatible("VecArr3", typeof(Vector3)), Is.True,
                "Vector3 field should be compatible with Vector3 stride=3 variable");
        }

        [Test]
        public void Vector3Field_Vector3Variable_Stride5_Compatible()
        {
            AddVariable<Vector3>("VecArr5", stride: 5);
            Assert.That(IsCompatible("VecArr5", typeof(Vector3)), Is.True,
                "Vector3 field should be compatible with Vector3 stride=5 variable");
        }

        // ═══════════════════════════════════════════════════════════════
        // Valid: Transform field → Transform variable
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void TransformField_TransformVariable_Stride1_Compatible()
        {
            AddVariable<Transform>("TxVar", stride: 1);
            Assert.That(IsCompatible("TxVar", typeof(Transform)), Is.True,
                "Transform field should be compatible with Transform stride=1 variable");
        }

        [Test]
        public void TransformField_TransformVariable_Stride3_Compatible()
        {
            AddVariable<Transform>("TxArr3", stride: 3);
            Assert.That(IsCompatible("TxArr3", typeof(Transform)), Is.True,
                "Transform field should be compatible with Transform stride=3 variable");
        }

        [Test]
        public void TransformField_TransformVariable_Stride5_Compatible()
        {
            AddVariable<Transform>("TxArr5", stride: 5);
            Assert.That(IsCompatible("TxArr5", typeof(Transform)), Is.True,
                "Transform field should be compatible with Transform stride=5 variable");
        }

        // ═══════════════════════════════════════════════════════════════
        // Valid: bool field → bool variable (extra type, stride=1 only)
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void BoolField_BoolVariable_Stride1_Compatible()
        {
            AddVariable<bool>("BoolVar", stride: 1);
            Assert.That(IsCompatible("BoolVar", typeof(bool)), Is.True,
                "bool field should be compatible with bool variable");
        }

        // ═══════════════════════════════════════════════════════════════
        // Invalid: cross-type mismatches
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void IntField_Vector3Variable_Stride1_Incompatible()
        {
            AddVariable<Vector3>("VecVar", stride: 1);
            Assert.That(IsCompatible("VecVar", typeof(int)), Is.False,
                "int field should NOT be compatible with Vector3 stride=1 variable");
        }

        [Test]
        public void IntField_Vector3Variable_Stride3_Incompatible()
        {
            AddVariable<Vector3>("VecArr3", stride: 3);
            Assert.That(IsCompatible("VecArr3", typeof(int)), Is.False,
                "int field should NOT be compatible with Vector3 stride=3 variable");
        }

        [Test]
        public void IntField_Vector3Variable_Stride5_Incompatible()
        {
            AddVariable<Vector3>("VecArr5", stride: 5);
            Assert.That(IsCompatible("VecArr5", typeof(int)), Is.False,
                "int field should NOT be compatible with Vector3 stride=5 variable");
        }

        [Test]
        public void FloatField_IntVariable_Stride1_Incompatible()
        {
            AddVariable<int>("IntVar", stride: 1);
            Assert.That(IsCompatible("IntVar", typeof(float)), Is.False,
                "float field should NOT be compatible with int variable");
        }

        [Test]
        public void FloatField_IntVariable_Stride3_Incompatible()
        {
            AddVariable<int>("IntArr3", stride: 3);
            Assert.That(IsCompatible("IntArr3", typeof(float)), Is.False,
                "float field should NOT be compatible with int stride=3 variable");
        }

        [Test]
        public void FloatField_IntVariable_Stride5_Incompatible()
        {
            AddVariable<int>("IntArr5", stride: 5);
            Assert.That(IsCompatible("IntArr5", typeof(float)), Is.False,
                "float field should NOT be compatible with int stride=5 variable");
        }

        [Test]
        public void Vector3Field_TransformVariable_Stride1_Incompatible()
        {
            AddVariable<Transform>("TxVar", stride: 1);
            Assert.That(IsCompatible("TxVar", typeof(Vector3)), Is.False,
                "Vector3 field should NOT be compatible with Transform variable");
        }

        [Test]
        public void Vector3Field_TransformVariable_Stride3_Incompatible()
        {
            AddVariable<Transform>("TxArr3", stride: 3);
            Assert.That(IsCompatible("TxArr3", typeof(Vector3)), Is.False,
                "Vector3 field should NOT be compatible with Transform stride=3 variable");
        }

        [Test]
        public void Vector3Field_TransformVariable_Stride5_Incompatible()
        {
            AddVariable<Transform>("TxArr5", stride: 5);
            Assert.That(IsCompatible("TxArr5", typeof(Vector3)), Is.False,
                "Vector3 field should NOT be compatible with Transform stride=5 variable");
        }

        [Test]
        public void TransformField_Vector3Variable_Stride1_Incompatible()
        {
            AddVariable<Vector3>("VecVar", stride: 1);
            Assert.That(IsCompatible("VecVar", typeof(Transform)), Is.False,
                "Transform field should NOT be compatible with Vector3 variable");
        }

        [Test]
        public void TransformField_Vector3Variable_Stride3_Incompatible()
        {
            AddVariable<Vector3>("VecArr3", stride: 3);
            Assert.That(IsCompatible("VecArr3", typeof(Transform)), Is.False,
                "Transform field should NOT be compatible with Vector3 stride=3 variable");
        }

        [Test]
        public void TransformField_Vector3Variable_Stride5_Incompatible()
        {
            AddVariable<Vector3>("VecArr5", stride: 5);
            Assert.That(IsCompatible("VecArr5", typeof(Transform)), Is.False,
                "Transform field should NOT be compatible with Vector3 stride=5 variable");
        }

        [Test]
        public void IntField_FloatVariable_Stride1_Incompatible()
        {
            AddVariable<float>("FloatVar", stride: 1);
            Assert.That(IsCompatible("FloatVar", typeof(int)), Is.False,
                "int field should NOT be compatible with float variable");
        }

        [Test]
        public void IntField_FloatVariable_Stride3_Incompatible()
        {
            AddVariable<float>("FloatArr3", stride: 3);
            Assert.That(IsCompatible("FloatArr3", typeof(int)), Is.False,
                "int field should NOT be compatible with float stride=3 variable");
        }

        // ═══════════════════════════════════════════════════════════════
        // Edge cases
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void UnknownVariable_ReturnsMinusOne()
        {
            var entry = MakeEntry("ghostField", "Nonexistent", typeof(int));
            int result = TreeBaker.ValidateVariableType(-1, entry, bbDef);
            Assert.That(result, Is.EqualTo(-1),
                "Unknown variable (varIndex=-1) should return -1");
        }
    }
}
