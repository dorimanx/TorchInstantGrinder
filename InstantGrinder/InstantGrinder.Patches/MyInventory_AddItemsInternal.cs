﻿using System;
using System.Reflection;
using Sandbox.Game;
using VRage;
using VRage.Game;

namespace InstantGrinder.Patches
{
    public static class MyInventory_AddItemsInternal
    {
        const string MethodName = "AddItemsInternal";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        static readonly Type[] ParameterTypes =
        {
            typeof(MyFixedPoint),
            typeof(MyObjectBuilder_PhysicalObject),
            typeof(uint?),
            typeof(int),
            typeof(bool),
            typeof(int),
            typeof(int),
        };

        delegate void MethodDelegate(MyInventory self, MyFixedPoint amount, MyObjectBuilder_PhysicalObject objectBuilder, uint? itemId, int index, bool simplifiedMassAndVolumeCalculation, int maxPositionIndex, int minPositionIndex);
        static readonly MethodInfo Method = typeof(MyInventory).GetMethod(MethodName, Flags, null, ParameterTypes, null);
        static readonly MethodDelegate MethodDelegateInstance = (MethodDelegate) Delegate.CreateDelegate(typeof(MethodDelegate), Method);

        public static void AddItemsInternal(
            this MyInventory self,
            MyObjectBuilder_PhysicalObject objectBuilder,
            MyFixedPoint amount)
        {
            MethodDelegateInstance(self, amount, objectBuilder, null, -1, false, -1, 0);
        }
    }
}