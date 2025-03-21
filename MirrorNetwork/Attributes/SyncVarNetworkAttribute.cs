using System;
using UnityEngine;

namespace Damntry.UtilsBepInEx.MirrorNetwork.Attributes;


[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class SyncVarNetworkAttribute : PropertyAttribute {

	public SyncVarNetworkAttribute() {}

}