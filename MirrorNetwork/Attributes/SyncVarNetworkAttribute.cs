using System;

namespace Damntry.UtilsBepInEx.MirrorNetwork.Attributes;


[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class SyncVarNetworkAttribute : Attribute {

	public SyncVarNetworkAttribute() {}

}