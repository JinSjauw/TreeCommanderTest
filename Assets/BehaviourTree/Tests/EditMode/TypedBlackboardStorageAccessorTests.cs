using System.Collections.Generic;
using BehaviourTree.Core;
using NUnit.Framework;
using UnityEngine;

namespace BehaviourTree.Tests
{
    public class TypedBlackboardStorageAccessorTests
    {
        private enum TestState { Idle = 0, Running = 1 }

        private static TypedBlackboardStorage CreateStorage()
        {
            var variables = new List<BlackboardVariableBase>
            {
                new BlackboardVariable<float>("f", 1, 1.5f),            // slot 0
                new BlackboardVariable<int>("i", 1, 7),                 // slot 1
                new BlackboardVariable<bool>("b", 1, true),             // slot 2
                new BlackboardVariable<Vector2>("v2", 1, Vector2.one),  // slot 3
                new BlackboardVariable<Vector3>("v3", 1, Vector3.one),  // slot 4
                new BlackboardVariable<Vector4>("v4", 1, Vector4.one),  // slot 5
                new BlackboardVariable<Color>("c", 1, Color.red),       // slot 6
                new BlackboardVariable<Quaternion>("q", 1, Quaternion.identity), // slot 7
                new BlackboardVariable<TestState>("e", 1, TestState.Running),    // slot 8 (enum → int array)
                new BlackboardVariable<string>("s", 1, "x"),            // slot 9 (object array)
            };
            var storage = new TypedBlackboardStorage();
            storage.Initialize(variables);
            return storage;
        }

        [Test]
        public void TypedGet_ReturnsSeededValues()
        {
            IBlackboardTypedAccess s = CreateStorage();
            Assert.AreEqual(1.5f, s.GetFloat(0));
            Assert.AreEqual(7, s.GetInt(1));
            Assert.AreEqual(true, s.GetBool(2));
            Assert.AreEqual(Vector2.one, s.GetVector2(3));
            Assert.AreEqual(Vector3.one, s.GetVector3(4));
            Assert.AreEqual(Vector4.one, s.GetVector4(5));
            Assert.AreEqual(Color.red, s.GetColor(6));
            Assert.AreEqual(Quaternion.identity, s.GetQuaternion(7));
            Assert.AreEqual((int)TestState.Running, s.GetInt(8));
            Assert.AreEqual("x", s.GetObject<string>(9));
        }

        [Test]
        public void TypedSet_Roundtrips()
        {
            IBlackboardTypedAccess s = CreateStorage();
            s.SetFloat(0, 9.25f);
            s.SetInt(1, 42);
            s.SetBool(2, false);
            s.SetVector2(3, new Vector2(2, 3));
            s.SetVector3(4, new Vector3(4, 5, 6));
            s.SetVector4(5, new Vector4(7, 8, 9, 10));
            s.SetColor(6, Color.blue);
            s.SetQuaternion(7, Quaternion.Euler(10, 20, 30));
            s.SetInt(8, (int)TestState.Idle);
            s.SetObject(9, "y");

            Assert.AreEqual(9.25f, s.GetFloat(0));
            Assert.AreEqual(42, s.GetInt(1));
            Assert.AreEqual(false, s.GetBool(2));
            Assert.AreEqual(new Vector2(2, 3), s.GetVector2(3));
            Assert.AreEqual(new Vector3(4, 5, 6), s.GetVector3(4));
            Assert.AreEqual(new Vector4(7, 8, 9, 10), s.GetVector4(5));
            Assert.AreEqual(Color.blue, s.GetColor(6));
            Assert.AreEqual(Quaternion.Euler(10, 20, 30), s.GetQuaternion(7));
            Assert.AreEqual(0, s.GetInt(8));
            Assert.AreEqual("y", s.GetObject<string>(9));
        }

        [Test]
        public void TypedAccess_AndBoxedAccess_Agree()
        {
            TypedBlackboardStorage storage = CreateStorage();
            IBlackboardTypedAccess s = storage;

            s.SetFloat(0, 3.5f);
            s.SetInt(8, (int)TestState.Idle);

            Assert.AreEqual(3.5f, storage.GetBoxed(0));
            Assert.AreEqual(3.5f, storage.Get<float>(0));
            Assert.IsInstanceOf<TestState>(storage.GetBoxed(8)); // enum identity invariant
            Assert.AreEqual(TestState.Idle, storage.Get<TestState>(8));
        }
    }
}
