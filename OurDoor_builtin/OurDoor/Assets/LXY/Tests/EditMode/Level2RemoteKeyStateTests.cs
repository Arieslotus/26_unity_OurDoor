/// <summary>
/// 实现功能：回归验证第二关 Outer 端钥匙落地后具备正确流程状态和 PC 可交互状态。
/// </summary>
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace OurDoor.LXY.Networking.Tests
{
    public sealed class Level2RemoteKeyStateTests
    {
        [Test]
        public void RemoteLandedStateMakesKeyPickupReadyForOuter()
        {
            Type keyType = RequireAssemblyCSharpType("SchoolKey");
            Type pickupType = RequireAssemblyCSharpType("PCPickupInteractable");
            GameObject keyObject = new GameObject("RemoteLandedKeyTest");
            GameObject targetObject = new GameObject("RemoteLandedKeyTarget");

            try
            {
                targetObject.transform.SetPositionAndRotation(
                    new Vector3(2f, 3f, 4f),
                    Quaternion.Euler(0f, 90f, 0f));

                Rigidbody body = keyObject.AddComponent<Rigidbody>();
                Component pickup = keyObject.AddComponent(pickupType);
                SetUnityEventField(pickupType, pickup, "onPickedUp");
                SetUnityEventField(pickupType, pickup, "onDropped");
                InvokeLifecycleMethod(pickupType, pickup, "Awake");

                LogAssert.Expect(LogType.Error, "no XRGrabInteractable on key");
                Component key = keyObject.AddComponent(keyType);
                InvokeLifecycleMethod(keyType, key, "Awake");
                FieldInfo targetPointField = keyType.GetField(
                    "targetPoint",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(targetPointField, Is.Not.Null, "SchoolKey 缺少 targetPoint 字段。");
                targetPointField.SetValue(key, targetObject.transform);

                MethodInfo applyMethod = keyType.GetMethod(
                    "ApplyRemoteLandedState",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(applyMethod, Is.Not.Null, "SchoolKey 缺少远端钥匙落地入口。");
                applyMethod.Invoke(key, null);

                Assert.That(ReadPrivateBool(keyType, key, "picked"), Is.True);
                Assert.That(ReadPrivateBool(keyType, key, "hasThrown"), Is.True);
                Assert.That(ReadPrivateBool(keyType, key, "hasPickedFromWall"), Is.False);
                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.useGravity, Is.True);
                Assert.That(keyObject.transform.position, Is.EqualTo(targetObject.transform.position));

                MethodInfo canInteractMethod = pickupType.GetMethod(
                    "CanInteract",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(canInteractMethod, Is.Not.Null);
                bool canInteract = (bool)canInteractMethod.Invoke(pickup, new object[] { null });
                Assert.That(canInteract, Is.True, "Outer 端落地钥匙仍不可交互。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(keyObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        private static Type RequireAssemblyCSharpType(string typeName)
        {
            Type type = Type.GetType($"{typeName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Assembly-CSharp 中缺少 {typeName}。");
            return type;
        }

        private static void InvokeLifecycleMethod(
            Type ownerType,
            object instance,
            string methodName)
        {
            MethodInfo method = ownerType.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                method,
                Is.Not.Null,
                $"{ownerType.Name} 缺少 {methodName} 生命周期方法。");
            method.Invoke(instance, null);
        }

        private static void SetUnityEventField(
            Type ownerType,
            object instance,
            string fieldName)
        {
            FieldInfo field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(
                field,
                Is.Not.Null,
                $"{ownerType.Name} 缺少 {fieldName} UnityEvent 字段。");
            field.SetValue(instance, new UnityEvent());
        }

        private static bool ReadPrivateBool(Type ownerType, object instance, string fieldName)
        {
            FieldInfo field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{ownerType.Name} 缺少字段 {fieldName}。");
            return (bool)field.GetValue(instance);
        }
    }
}
